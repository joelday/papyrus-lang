import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { take } from 'rxjs/operators';
import { IPathResolver } from '../common/PathResolver';
import { ILanguageClientManager } from '../server/LanguageClientManager';

import * as fs from 'fs';
import { promisify } from 'util';

import { IDebugSupportInstallService, DebugSupportInstallState } from './DebugSupportInstallService';
import { IAddressLibraryInstallService, AddressLibInstalledState } from './AddressLibInstallService';
import { MO2LauncherDescriptor } from './MO2LaunchDescriptorFactory';
import {
    AddRequiredModsToModList,
    checkAddressLibrariesExistAndEnabled,
    checkPDSModExistsAndEnabled,
    isMO2Running,
    isOurMO2Running,
    killAllMO2Processes,
} from './MO2Helpers';
import * as MO2Lib from '../common/MO2Lib';
import { CancellationTokenSource } from 'vscode-languageclient';
import { CancellationToken } from 'vscode';
import { execFile as _execFile, spawn } from 'child_process';
const execFile = promisify(_execFile);
const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

export enum MO2LaunchConfigurationStatus {
    Ready = 0,
    // fixable
    PDSNotInstalled = 1 << 0,
    PDSIncorrectVersion = 1 << 1,
    AddressLibraryNotInstalled = 1 << 2,
    AddressLibraryOutdated = 1 << 3, // This is not currently in use
    PDSModNotEnabledInModList = 1 << 4,
    AddressLibraryModNotEnabledInModList = 1 << 5,
    // not fixable
    ModListNotParsable = 1 << 6,
    UnknownError = 1 << 7,
}

export interface IMO2ConfiguratorService {
    getStateFromConfig(launchDescriptor: MO2LauncherDescriptor): Promise<MO2LaunchConfigurationStatus>;
    fixDebuggerConfiguration(
        launchDescriptor: MO2LauncherDescriptor,
        cancellationToken?: CancellationToken
    ): Promise<boolean>;
}
function _getErrorMessage(state: MO2LaunchConfigurationStatus) {
    switch (state) {
        case MO2LaunchConfigurationStatus.Ready:
            return 'Ready';
        case MO2LaunchConfigurationStatus.PDSNotInstalled:
            return 'Papyrus Debug Support is not installed';
        case MO2LaunchConfigurationStatus.PDSIncorrectVersion:
            return 'Papyrus Debug Support is not the correct version';
        case MO2LaunchConfigurationStatus.AddressLibraryNotInstalled:
            return 'Address Library is not installed';
        case MO2LaunchConfigurationStatus.AddressLibraryOutdated: // This is not currently in use
            return 'Address Library is not the correct version';
        case MO2LaunchConfigurationStatus.PDSModNotEnabledInModList:
            return 'Papyrus Debug Support mod is not enabled in the mod list';
        case MO2LaunchConfigurationStatus.AddressLibraryModNotEnabledInModList:
            return 'Address Library mod is not enabled in the mod list';
        case MO2LaunchConfigurationStatus.ModListNotParsable:
            return 'Mod list is not parsable';
        case MO2LaunchConfigurationStatus.UnknownError:
            return 'An unknown error';
    }
    return 'An unknown error';
}

export function GetErrorMessageFromStatus(state: MO2LaunchConfigurationStatus): string {
    const errorMessages = new Array<string>();
    const states = getStates(state);
    if (states.length === 1 && states[0] === MO2LaunchConfigurationStatus.Ready) {
        return 'Ready';
    }
    for (const state of states) {
        errorMessages.push(_getErrorMessage(state));
    }
    const errMsg = '- ' + errorMessages.join('\n - ');
    return errMsg;
}
function getStates(state: MO2LaunchConfigurationStatus): MO2LaunchConfigurationStatus[] {
    if (state === MO2LaunchConfigurationStatus.Ready) {
        return [MO2LaunchConfigurationStatus.Ready];
    }
    const states: MO2LaunchConfigurationStatus[] = [];
    let key: keyof typeof MO2LaunchConfigurationStatus;
    for (key in MO2LaunchConfigurationStatus) {
        const value: MO2LaunchConfigurationStatus = Number(MO2LaunchConfigurationStatus[key]);
        if (state & value) {
            states.push(value);
        }
    }
    return states;
}

@injectable()
export class MO2ConfiguratorService implements IMO2ConfiguratorService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _pathResolver: IPathResolver;
    private readonly _debugSupportInstallService: IDebugSupportInstallService;
    private readonly _addressLibraryInstallService: IAddressLibraryInstallService;

    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver,
        @inject(IDebugSupportInstallService) debugSupportInstallService: IDebugSupportInstallService,
        @inject(IAddressLibraryInstallService) addressLibraryInstallService: IAddressLibraryInstallService
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
        this._debugSupportInstallService = debugSupportInstallService;
        this._addressLibraryInstallService = addressLibraryInstallService;
    }
    public static errorIsRecoverable(state: MO2LaunchConfigurationStatus): boolean {
        if (state === MO2LaunchConfigurationStatus.Ready) {
            return true;
        }
        if (
            state & MO2LaunchConfigurationStatus.ModListNotParsable ||
            state & MO2LaunchConfigurationStatus.UnknownError
        ) {
            return false;
        }
        return true;
    }

    public async getStateFromConfig(launchDescriptor: MO2LauncherDescriptor): Promise<MO2LaunchConfigurationStatus> {
        let state = MO2LaunchConfigurationStatus.Ready;
        state |= await this.checkPDSisPresent(launchDescriptor);
        state |= await this.checkAddressLibsArePresent(launchDescriptor);
        state |= await this.checkModListHasPDSEnabled(launchDescriptor);
        state |= await this.checkModListHasAddressLibsEnabled(launchDescriptor);
        return state;
    }

    private async checkPDSisPresent(launchDescriptor: MO2LauncherDescriptor): Promise<MO2LaunchConfigurationStatus> {
        const result = await this._debugSupportInstallService.getInstallState(
            launchDescriptor.game,
            launchDescriptor.instanceInfo.modsFolder
        );
        const ignoreVersion = (await this._configProvider.config.pipe(take(1)).toPromise())[launchDescriptor.game]
            .ignoreDebuggerVersion;
        if (result !== DebugSupportInstallState.installed && result !== DebugSupportInstallState.installedAsMod) {
            if (result === DebugSupportInstallState.incorrectVersion) {
                if (ignoreVersion) {
                    return MO2LaunchConfigurationStatus.Ready;
                }
                return MO2LaunchConfigurationStatus.PDSIncorrectVersion;
            }
            // TODO: care about the other states?
            return MO2LaunchConfigurationStatus.PDSNotInstalled;
        }
        return MO2LaunchConfigurationStatus.Ready;
    }

    private async checkAddressLibsArePresent(
        launchDescriptor: MO2LauncherDescriptor
    ): Promise<MO2LaunchConfigurationStatus> {
        const result = await this._addressLibraryInstallService.getInstallState(
            launchDescriptor.game,
            launchDescriptor.instanceInfo.modsFolder
        );
        if (result === AddressLibInstalledState.notInstalled) {
            return MO2LaunchConfigurationStatus.AddressLibraryNotInstalled;
        } else if (result === AddressLibInstalledState.outdated) {
            // not currently in use
            return MO2LaunchConfigurationStatus.AddressLibraryOutdated;
        } // we don't care about installedButCantCheckForUpdates
        return MO2LaunchConfigurationStatus.Ready;
    }

    // Check if the MO2 modlist has the PDS mod and the Address Library mod enabled
    private async checkModListHasPDSEnabled(
        launchDescriptor: MO2LauncherDescriptor
    ): Promise<MO2LaunchConfigurationStatus> {
        const modList = await MO2Lib.ParseModListFile(launchDescriptor.profileToLaunchData.modListPath);
        // The descriptor factory checked the path and the data was parsable, so this should never happen
        if (!modList) {
            return MO2LaunchConfigurationStatus.ModListNotParsable;
        }
        const ret: MO2LaunchConfigurationStatus = MO2LaunchConfigurationStatus.Ready;
        if (!checkPDSModExistsAndEnabled(modList)) {
            return MO2LaunchConfigurationStatus.PDSModNotEnabledInModList;
        }
        return MO2LaunchConfigurationStatus.Ready;
    }

    private async checkModListHasAddressLibsEnabled(
        launchDescriptor: MO2LauncherDescriptor
    ): Promise<MO2LaunchConfigurationStatus> {
        const modList = await MO2Lib.ParseModListFile(launchDescriptor.profileToLaunchData.modListPath);
        // The descriptor factory checked the path and the data was parsable, so this should never happen
        if (!modList) {
            return MO2LaunchConfigurationStatus.ModListNotParsable;
        }
        if (!checkAddressLibrariesExistAndEnabled(modList, launchDescriptor.game)) {
            return MO2LaunchConfigurationStatus.AddressLibraryModNotEnabledInModList;
        }

        return MO2LaunchConfigurationStatus.Ready;
    }

    public async fixDebuggerConfiguration(
        launchDescriptor: MO2LauncherDescriptor,
        cancellationToken = new CancellationTokenSource().token
    ): Promise<boolean> {
        const states = getStates(await this.getStateFromConfig(launchDescriptor));
        for (const state of states) {
            switch (state) {
                case MO2LaunchConfigurationStatus.Ready:
                    break;
                case MO2LaunchConfigurationStatus.PDSNotInstalled:
                case MO2LaunchConfigurationStatus.PDSIncorrectVersion:
                    if (
                        !(await this._debugSupportInstallService.installPlugin(
                            launchDescriptor.game,
                            cancellationToken,
                            launchDescriptor.instanceInfo.modsFolder
                        ))
                    ) {
                        return false;
                    }
                    break;
                case MO2LaunchConfigurationStatus.AddressLibraryNotInstalled:
                case MO2LaunchConfigurationStatus.AddressLibraryOutdated:
                    if (
                        !(await this._addressLibraryInstallService.installLibraries(
                            launchDescriptor.game,
                            true, // force download
                            cancellationToken,
                            launchDescriptor.instanceInfo.modsFolder
                        ))
                    ) {
                        return false;
                    }
                    break;
                case MO2LaunchConfigurationStatus.PDSModNotEnabledInModList:
                case MO2LaunchConfigurationStatus.AddressLibraryModNotEnabledInModList:
                    let wasRunning = false;
                    // if MO2 is running, we have to force a refresh after we add the mods, or it will overwrite our changes
                    if (await isMO2Running()) {
                        wasRunning = true;
                        // if ModOrganizer is currently running, and the installation or selected profile isn't what we're going to run, this will fuck up, kill it
                        const notOurs = !(await isOurMO2Running(launchDescriptor.MO2EXEPath));
                        if (
                            notOurs ||
                            launchDescriptor.instanceInfo.selectedProfile !== launchDescriptor.profileToLaunchData.name
                        ) {
                            await killAllMO2Processes();
                        }
                    }

                    const modList = await MO2Lib.ParseModListFile(launchDescriptor.profileToLaunchData.modListPath);
                    if (!modList) {
                        return false;
                    }

                    const newmodList = AddRequiredModsToModList(modList, launchDescriptor.game);
                    if (
                        !MO2Lib.WriteChangesToModListFile(launchDescriptor.profileToLaunchData.modListPath, newmodList)
                    ) {
                        return false;
                    }
                    if (wasRunning) {
                        spawn(
                            launchDescriptor.MO2EXEPath,
                            ['-p', launchDescriptor.profileToLaunchData.name, 'refresh'],
                            {
                                detached: true,
                                stdio: 'ignore',
                            }
                        ).unref();
                    }
                    break;
                default:
                    // shouldn't reach here
                    throw new Error(`Unknown state in fixDebuggerConfiguration`);
            }
            if (state === MO2LaunchConfigurationStatus.Ready) {
                break;
            }
        }

        return true;
    }
}

export const IMO2ConfiguratorService: interfaces.ServiceIdentifier<IMO2ConfiguratorService> =
    Symbol('mo2ConfiguratorService');
