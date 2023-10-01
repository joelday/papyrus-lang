import { existsSync, fstat, openSync, readFileSync, writeFileSync } from 'fs';
import * as ini from 'ini';
import { PapyrusGame } from '../PapyrusGame';
import { MO2Config } from './PapyrusDebugSession';
import { getExecutableNameForGame, IPathResolver, PathResolver } from '../common/PathResolver';
import { PDSModName } from '../common/constants';

import { IExtensionConfigProvider } from '../ExtensionConfigProvider';

import { inject, injectable } from 'inversify';
import path from 'path';
import {
    getGameIniPath,
    FindMO2ProfilesFolder,
    ParseIniFile,
    CheckIfDebuggingIsEnabledInIni,
    TurnOnDebuggingInIni,
    WriteChangesToIni,
    ParseModListFile,
    checkPDSModExistsAndEnabled,
    checkAddressLibraryExistsAndEnabled,
    parseMoshortcutURI,
    AddRequiredModsToModList,
    WriteChangesToModListFile,
    ModListItem,
    INIData,
    FindMO2InstanceIniPath,
    GetGlobalGameSavedDataFolder,
    getRelativePluginPath,
    getGameIniName,
} from './MO2Helpers';
import { IDebugSupportInstallService, DebugSupportInstallState } from './DebugSupportInstallService';

export enum MO2LaunchConfigurationState {
    Ready = 0,
    PDSNotInstalled = 1 >> 0,
    PDSIncorrectVersion = 1 >> 1,
    AddressLibraryNotInstalled = 1 >> 2,
    GameIniNotSetupForDebugging = 1 >> 4,
    PDSModNotEnabledInModList = 1 >> 5,
    AddressLibraryModNotEnabledInModList = 1 >> 6,
}

export interface MO2ProfileData {
    name: string;
    folderPath: string;
    /**
     * Path to the ini file that contains the settings for this profile
     * Should always be present in profile folder and should always be named "settings.ini"
     * @type {string}
     */
    settingsIniPath: string;
    settingsIniData: INIData;
    /**
     * Path to the txt file that contains the mod list for this profile
     * Should always be present in profile folder and should always be named "modlist.txt"
     * @type {string}
     */
    modListPath: string;
    modListData: ModListItem[];
    /**
     * Path to the ini file that contains the Skyrim or Fallout 4 settings.
     * Depending if the profile has local settings, this is either present in the profile folder or in the global save game folder.
     * Should alays be named "Skyrim.ini" or "Fallout4.ini"
     * @type {string}
     */
    gameIniPath: string;
    gameIniData: INIData;
}
export interface MO2InstanceData {
    name: string;
    modsFolder: string;
    profilesFolder: string;
    selectedProfileData: MO2ProfileData;
}
export function GetErrorMessageFromState(state: MO2LaunchConfigurationState): string {
    let errorMessages = new Array<string>();
    switch (state) {
        case MO2LaunchConfigurationState.Ready:
            return 'Ready';
        case state & MO2LaunchConfigurationState.PDSNotInstalled:
            errorMessages.push('Papyrus Debug Support is not installed');
        case MO2LaunchConfigurationState.PDSIncorrectVersion:
            errorMessages.push('Papyrus Debug Support is not the correct version');
        case MO2LaunchConfigurationState.AddressLibraryNotInstalled:
            errorMessages.push('Address Library is not installed');
        case MO2LaunchConfigurationState.GameIniNotSetupForDebugging:
            errorMessages.push('Game ini is not setup for debugging');
        case MO2LaunchConfigurationState.PDSModNotEnabledInModList:
            errorMessages.push('Papyrus Debug Support mod is not enabled in the mod list');
        case MO2LaunchConfigurationState.AddressLibraryModNotEnabledInModList:
            errorMessages.push('Address Library mod is not enabled in the mod list');
        default:
    }
    const errMsg = 'The following errors were found: \n -' + errorMessages.join('\n -');
    return errMsg;
}

@injectable()
export class MO2LaunchConfigurationFactory {
    private readonly _debugSupportInstallService: IDebugSupportInstallService;

    constructor(@inject(IDebugSupportInstallService) debugSupportInstallService: IDebugSupportInstallService) {
        this._debugSupportInstallService = debugSupportInstallService;
    }
    // TODO: After testing, make these private
    public static async populateMO2ProfileData(
        name: string,
        profileFolder: string,
        game: PapyrusGame
    ): Promise<MO2ProfileData> {
        if (!existsSync(profileFolder)) {
            throw new Error(`Could not find the profile folder ${profileFolder}}`);
        }

        // settings.ini should always be present in profiles
        const settingsIniPath = path.join(profileFolder, 'settings.ini');
        if (!existsSync(settingsIniPath)) {
            throw new Error(`Could not find the settings.ini file in ${profileFolder}}`);
        }
        const settingsIniData = await ParseIniFile(settingsIniPath);
        if (
            !settingsIniData ||
            settingsIniData.General === undefined ||
            settingsIniData.General.LocalSettings === undefined
        ) {
            throw new Error(`MO2 profile Settings ini file ${settingsIniPath} is not parsable`);
        }

        // Game ini paths for MO2 are different depending on whether the profile has local settings or not
        // if [General] LocalSettings=false, then the game ini is in the global game save folder
        // if [General] LocalSettings=true, then the game ini is in the profile folder
        const gameIniName = getGameIniName(game);
        let gameIniPath: string;
        if (settingsIniData.General.LocalSettings === false) {
            // We don't have local game ini settings, so we need to use the global ones
            let gameSaveDir = GetGlobalGameSavedDataFolder(game);
            if (!gameSaveDir) {
                throw new Error(`Could not find the Global ${game} save directory`);
            }
            if (!existsSync(gameSaveDir)) {
                throw new Error(
                    `MO2 profile does not have local game INI settings, but could not find the global game save directory at ${gameSaveDir}`
                );
            }
            gameIniPath = path.join(gameSaveDir, gameIniName);
            if (!existsSync(gameIniPath)) {
                throw new Error(
                    `MO2 profile does not have local game INI settings, but could not find the global game ${game} ini @ ${gameIniPath} (Try running the game once to generate the ini file)`
                );
            }
        } else {
            gameIniPath = getGameIniPath(profileFolder, game);
            if (!existsSync(gameIniPath)) {
                throw new Error(
                    `MO2 profile has local game INI settings, but could not find the local ${game} ini @ ${gameIniPath}`
                );
            }
        }

        if (!existsSync(gameIniPath)) {
            throw new Error(`Could not find the skyrim.ini file @ ${gameIniPath}`);
        }
        const gameIniData = await ParseIniFile(gameIniPath);
        if (!gameIniData) {
            throw new Error(`Game ini file is not parsable`);
        }
        const ModsListPath = path.join(profileFolder, 'modlist.txt');
        if (!existsSync(ModsListPath)) {
            throw new Error(`Could not find the modlist.txt file`);
        }
        const ModsListData = await ParseModListFile(ModsListPath);
        if (!ModsListData) {
            throw new Error(`Mod list file is not parsable`);
        }
        return {
            name: name,
            folderPath: profileFolder,
            settingsIniPath: settingsIniPath,
            settingsIniData: settingsIniData,
            modListPath: ModsListPath,
            modListData: ModsListData,
            gameIniPath: gameIniPath,
            gameIniData: gameIniData,
        } as MO2ProfileData;
    }

    public static async populateMO2InstanceData(mo2Config: MO2Config, game: PapyrusGame): Promise<MO2InstanceData> {
        // taken care of by debug config provider
        const modsFolder = mo2Config.modsFolder;
        if (!mo2Config.modsFolder) {
            throw new Error(`Mod directory path is not set`);
        }
        // TODO: Have the debug config provider check this before we get here
        const { instanceName, exeName } = parseMoshortcutURI(mo2Config.shortcut);
        if (!instanceName || !exeName) {
            throw new Error(`Could not parse the shortcut URI ${mo2Config.shortcut}}`);
        }

        let profilesFolder = mo2Config.profilesFolder;
        // TODO: Consider moving this to the DebugConfigProvider
        if (!profilesFolder) {
            // Try the parent folder of the mods folder
            profilesFolder = path.join(mo2Config.modsFolder, '..', 'profiles');
            // If it's not there, then we have to parse the MO2 ini to find the profiles folder
            if (!existsSync(profilesFolder)) {
                // Instance directories are always where ModOrganizer.ini is located
                const InstanceIniPath = await FindMO2InstanceIniPath(
                    mo2Config.modsFolder,
                    mo2Config.MO2EXEPath,
                    mo2Config.profile
                );
                if (!InstanceIniPath || !existsSync(InstanceIniPath)) {
                    throw new Error(`Profiles Folder not set, but could not find the instance.ini file`);
                }

                const InstanceDirectory = path.dirname(InstanceIniPath);

                const InstanceIniData = await ParseIniFile(InstanceIniPath);
                if (!InstanceIniData) {
                    throw new Error(
                        `Profiles Folder not set, but instance ini file at ${InstanceIniPath} is not parsable`
                    );
                }
                profilesFolder = await FindMO2ProfilesFolder(mo2Config.modsFolder, InstanceIniData);
                if (!profilesFolder) {
                    throw new Error(
                        `Profiles Folder not set, but could not find the "profiles" folder in the instance directory ${InstanceDirectory}`
                    );
                }
            }
        }
        if (existsSync(profilesFolder)) {
            throw new Error(`Could not find the "profiles" folder: ${profilesFolder}`);
        }

        // taken care of by debug config provider
        const profileName = mo2Config.profile;
        if (!profileName) {
            throw new Error(`Profile name is not set`);
        }

        const profileFolder = path.join(profilesFolder, profileName);
        if (!existsSync(profileFolder)) {
            throw new Error(`Could not find profile folder ${profileName} in ${profilesFolder}`);
        }

        let selectedProfileData: MO2ProfileData;
        try {
            selectedProfileData = await this.populateMO2ProfileData(profileName, profileFolder, game);
        } catch (error) {
            throw new Error(`Could not populate the profile data: ${error}`);
        }

        return {
            name: instanceName,
            modsFolder: modsFolder,
            profilesFolder: profilesFolder,
            selectedProfileData: selectedProfileData,
        } as MO2InstanceData;
    }

    public static async populateMO2LaunchConfiguration(
        mo2Config: MO2Config,
        game: PapyrusGame
    ): Promise<IMO2LauncherDescriptor> {
        // taken care of by debug config provider
        if (!mo2Config.modsFolder) {
            throw new Error(`Mod directory path is not set`);
        }
        if (!existsSync(mo2Config.modsFolder)) {
            throw new Error(`Mod directory path does not exist`);
        }

        // TODO: make the debug config provider do this
        if (!mo2Config.profile) {
            throw new Error(`Profile is not set`);
        }

        let { instanceName, exeName } = parseMoshortcutURI(mo2Config.shortcut);
        if (!instanceName || !exeName) {
            throw new Error(`Could not parse the shortcut URI`);
        }

        let MO2EXEPath = mo2Config.MO2EXEPath;
        if (!MO2EXEPath || !existsSync(MO2EXEPath)) {
            throw new Error(`Could not find the Mod Organizer 2 executable path`);
        }
        let instanceData: MO2InstanceData;
        try {
            instanceData = await this.populateMO2InstanceData(mo2Config, game);
        } catch (error) {
            throw new Error(`Could not populate the instance data: ${error}`);
        }
        const args = mo2Config.args || [];
        return {
            exeName,
            MO2EXEPath,
            args,
            game,
            instanceData,
        } as IMO2LauncherDescriptor;
    }

    public async createMO2LaunchDecriptor(mo2Config: MO2Config, game: PapyrusGame): Promise<MO2LaunchDescriptor> {
        let idescriptor: IMO2LauncherDescriptor;
        try {
            idescriptor = await MO2LaunchConfigurationFactory.populateMO2LaunchConfiguration(mo2Config, game);
        } catch (error) {
            throw new Error(`Could not create the launch configuration: ${error}`);
        }

        return new MO2LaunchDescriptor(
            idescriptor.exeName,
            idescriptor.MO2EXEPath,
            idescriptor.args,
            idescriptor.game,
            idescriptor.instanceData,
            this._debugSupportInstallService
        );
    }
}

export interface LaunchCommand {
    command: string;
    args: string[];
}

export interface IMO2LauncherDescriptor {
    exeName: string;
    MO2EXEPath: string;
    args: string[];
    game: PapyrusGame;
    instanceData: MO2InstanceData;
    checkIfDebuggerConfigured(): Promise<MO2LaunchConfigurationState>;
    fixDebuggerConfiguration(): Promise<boolean>;
    getLaunchCommand(): LaunchCommand;
}

export class MO2LaunchDescriptor implements IMO2LauncherDescriptor {
    public readonly exeName: string;
    public readonly MO2EXEPath: string;
    public readonly args: string[];
    public readonly game: PapyrusGame;
    public readonly instanceData: MO2InstanceData;

    // TODO: Refactor this to not use this
    private readonly _debugSupportInstallService: IDebugSupportInstallService;

    constructor(
        exeName: string,
        MO2EXEPath: string,
        args: string[],
        game: PapyrusGame,
        instanceData: MO2InstanceData,
        debugSupportInstallService: IDebugSupportInstallService
    ) {
        this.exeName = exeName;
        this.MO2EXEPath = MO2EXEPath;
        this.args = args;
        this.game = game;
        this.instanceData = instanceData;
        this._debugSupportInstallService = debugSupportInstallService;
    }

    public async checkIfDebuggerConfigured(): Promise<MO2LaunchConfigurationState> {
        let ret: MO2LaunchConfigurationState =
            (await this.checkIfModsArePresent()) |
            (await this.checkIfGameIniIsCorrectlyConfigured()) |
            (await this.checkIfMO2IsCorrectlyConfigured());
        return ret;
    }

    public async fixDebuggerConfiguration(): Promise<boolean> {
        let state = await this.checkIfDebuggerConfigured();
        while (state !== MO2LaunchConfigurationState.Ready) {
            switch (state) {
                case MO2LaunchConfigurationState.Ready:
                    break;
                case state & MO2LaunchConfigurationState.PDSNotInstalled:
                case state & MO2LaunchConfigurationState.PDSIncorrectVersion:
                    let relativePluginPath = getRelativePluginPath(this.game);
                    if (
                        !(await this._debugSupportInstallService.installPlugin(
                            this.game,
                            undefined,
                            path.join(this.instanceData.modsFolder, PDSModName, relativePluginPath)
                        ))
                    ) {
                        return false;
                    }
                    state &= ~MO2LaunchConfigurationState.PDSNotInstalled;
                    state &= ~MO2LaunchConfigurationState.PDSIncorrectVersion;
                    break;
                case state & MO2LaunchConfigurationState.GameIniNotSetupForDebugging:
                    const inidata = await ParseIniFile(this.instanceData.selectedProfileData.gameIniPath);
                    if (!inidata) {
                        return false;
                    }
                    const newGameIni = TurnOnDebuggingInIni(inidata);
                    if (!WriteChangesToIni(this.instanceData.selectedProfileData.gameIniPath, newGameIni)) {
                        return false;
                    }
                    this.instanceData.selectedProfileData.gameIniData = newGameIni;
                    state &= ~MO2LaunchConfigurationState.GameIniNotSetupForDebugging;
                    break;
                case state & MO2LaunchConfigurationState.PDSModNotEnabledInModList:
                case state & MO2LaunchConfigurationState.AddressLibraryModNotEnabledInModList:
                    const modList = await ParseModListFile(this.instanceData.selectedProfileData.modListPath);
                    if (!modList) {
                        return false;
                    }
                    const newmodList = AddRequiredModsToModList(modList, this.game);
                    if (!WriteChangesToModListFile(this.instanceData.selectedProfileData.modListPath, modList)) {
                        return false;
                    }
                    this.instanceData.selectedProfileData.modListData = newmodList;
                    state &= ~MO2LaunchConfigurationState.PDSModNotEnabledInModList;
                    state &= ~MO2LaunchConfigurationState.AddressLibraryModNotEnabledInModList;
                    break;
                default:
                    // shouldn't reach here
                    throw new Error(`Unknown state in fixDebuggerConfiguration`);
            }
            if (state === MO2LaunchConfigurationState.Ready) {
                break;
            }
        }
        return true;
    }
    public getLaunchCommand(): LaunchCommand {
        let command = this.MO2EXEPath;
        let cmdargs = ['-p', this.instanceData.selectedProfileData.name];
        if (this.instanceData.name !== 'portable') {
            cmdargs = cmdargs.concat(['-i', this.instanceData.name]);
        }
        cmdargs.concat('-e', this.exeName);
        if (this.args) {
            cmdargs = cmdargs.concat(['-a'].concat(this.args));
        }
        return {
            command: command,
            args: cmdargs,
        } as LaunchCommand;
    }

    public async checkIfModsArePresent(): Promise<MO2LaunchConfigurationState> {
        // TODO: Change this to not have to read global state
        let result = await this._debugSupportInstallService.getInstallState(this.game, this.instanceData.modsFolder);
        if (result !== DebugSupportInstallState.installed) {
            if (result === DebugSupportInstallState.notInstalled) {
                return MO2LaunchConfigurationState.PDSNotInstalled;
            }
            if (result === DebugSupportInstallState.incorrectVersion) {
                return MO2LaunchConfigurationState.PDSIncorrectVersion;
            } else {
                // TODO : FIX THIS
                throw new Error(`Unknown result from getInstallState`);
            }
        }
        return MO2LaunchConfigurationState.Ready;
    }

    async checkIfGameIniIsCorrectlyConfigured(): Promise<MO2LaunchConfigurationState> {
        if (!CheckIfDebuggingIsEnabledInIni(this.instanceData.selectedProfileData.gameIniData)) {
            return MO2LaunchConfigurationState.GameIniNotSetupForDebugging;
        }
        return MO2LaunchConfigurationState.Ready;
    }

    // Check if the MO2 modlist has the PDS mod and the Address Library mod enabled
    async checkIfMO2IsCorrectlyConfigured(): Promise<MO2LaunchConfigurationState> {
        let ret: MO2LaunchConfigurationState = MO2LaunchConfigurationState.Ready;
        if (!checkPDSModExistsAndEnabled(this.instanceData.selectedProfileData.modListData)) {
            ret |= MO2LaunchConfigurationState.PDSModNotEnabledInModList;
        }
        if (!checkAddressLibraryExistsAndEnabled(this.instanceData.selectedProfileData.modListData, this.game)) {
            ret |= MO2LaunchConfigurationState.AddressLibraryModNotEnabledInModList;
        }
        return ret;
    }
}
