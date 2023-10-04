import { inject, injectable } from 'inversify';
import { DebugConfigurationProvider, CancellationToken, WorkspaceFolder, debug, Disposable, commands } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame, getScriptExtenderExecutableName } from '../PapyrusGame';
import { GetPapyrusGameFromMO2GameID } from './MO2Helpers';
import { FindInstanceForEXE, parseMoshortcutURI } from '../common/MO2Lib';
import { MO2Config, IPapyrusDebugConfiguration } from './PapyrusDebugSession';
import { getHomeFolder, getLocalAppDataFolder, getTempFolder, getUserName } from '../common/OSHelpers';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import path from 'path';
import { promisify } from 'util';
import fs from 'fs';
const exists = promisify(fs.exists);

// Possibly based on custom language server requests:
// TODO: Resolve project from whichever that includes the active editor file.
// TODO: Provide configurations based on .ppj files in current directory.

@injectable()
export class PapyrusDebugConfigurationProvider implements DebugConfigurationProvider, Disposable {
    private readonly _registration: Disposable;
    private readonly _pathResolver: IPathResolver;
    private readonly _configProvider: IExtensionConfigProvider;

    private readonly DebuggerGames = [PapyrusGame.skyrimSpecialEdition, PapyrusGame.fallout4, PapyrusGame.starfield];
    private readonly _registrations: Disposable[];
    constructor(
        @inject(IPathResolver) pathResolver: IPathResolver,
        @inject(IExtensionConfigProvider) configurationservice: IExtensionConfigProvider
    ) {
        const supportedGames = [PapyrusGame.fallout4, PapyrusGame.skyrim, PapyrusGame.starfield];
        this._registrations = [
            commands.registerCommand(
                `papyrus.debugConfig.getInstallDir`,
                async (config: IPapyrusDebugConfiguration) => {
                    let thing = config;

                    return await this.getConfiguredGameDir(config.game);
                }
            ),
        ];

        this._pathResolver = pathResolver;
        this._configProvider = configurationservice;
        this._registration = debug.registerDebugConfigurationProvider('papyrus', this);
    }

    fuck() {
        return 'FUCK!!!!!';
    }

    async provideDebugConfigurations(
        _folder: WorkspaceFolder | undefined,
        _token?: CancellationToken
        // TODO: FIX THIS
    ): Promise<IPapyrusDebugConfiguration[]> {
        const PapyrusAttach = {
            type: 'papyrus',
            name: 'Fallout 4',
            game: PapyrusGame.fallout4,
            request: 'attach',
            projectPath: '${workspaceFolder}/fallout4.ppj',
        } as IPapyrusDebugConfiguration;
        const PapyrusMO2Launch = {
            type: 'papyrus',
            name: 'Fallout 4 (Launch with MO2)',
            game: PapyrusGame.fallout4,
            request: 'launch',
            launchType: 'MO2',
            launcherPath: 'C:/Modding/MO2/ModOrganizer.exe',
            mo2Config: {
                shortcutURI: 'moshortcut://Fallout 4:F4SE',
            } as MO2Config,
        } as IPapyrusDebugConfiguration;
        const PapyruseXSELaunch = {
            type: 'papyrus',
            name: 'Fallout 4 (Launch with F4SE)',
            game: PapyrusGame.fallout4,
            request: 'launch',
            launchType: 'XSE',
            launcherPath: 'C:/Program Files (x86)/Steam/steamapps/common/Fallout 4/f4se_loader.exe',
            args: ['-skipIntro'],
        } as IPapyrusDebugConfiguration;
        return [PapyrusMO2Launch, PapyruseXSELaunch, PapyrusAttach];
    }

    async resolveDebugConfiguration(
        _folder: WorkspaceFolder | undefined,
        debugConfiguration: IPapyrusDebugConfiguration,
        _token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration | null | undefined> {
        if (debugConfiguration.game === undefined) {
            throw new Error('Invalid debug configuration: Missing "game" field.');
        }
        if (!this.DebuggerGames.includes(debugConfiguration.game)) {
            'Invalid debug configuration: Invalid "game" field, we only support: ' + this.DebuggerGames.join(' ');
        }
        if (debugConfiguration.game !== undefined && debugConfiguration.request !== undefined) {
            if (debugConfiguration.request === 'launch') {
                if (debugConfiguration.launchType === 'MO2') {
                    if (
                        debugConfiguration.mo2Config !== undefined &&
                        debugConfiguration.mo2Config.shortcutURI !== undefined
                    ) {
                        return debugConfiguration;
                    }
                } else if (debugConfiguration.launchType === 'XSE') {
                    return debugConfiguration;
                }
            } else if (debugConfiguration.request === 'attach') {
                return debugConfiguration;
            }
        }
        throw new Error('Invalid debug configuration.');
    }

    // TODO: We might not want to do this
    // substitute all the environment variables in the given string
    // environment variables are of the form ${env:VARIABLE_NAME}
    async substituteEnvVars(string: string): Promise<string> {
        const envVars = string.match(/\$\{env:([^}]+)\}/g);
        if (envVars !== null) {
            for (const envVar of envVars) {
                if (envVar === undefined || envVar === null) {
                    continue;
                }
                const matches = envVar?.match(/\$\{env:([^}]+)\}/);
                if (matches === null || matches.length < 2) {
                    continue;
                }
                const envVarName = matches[1];
                let envVarValue: string | undefined;

                switch (envVarName) {
                    case 'LOCALAPPDATA':
                        envVarValue = getLocalAppDataFolder();
                        break;
                    case 'USERPROFILE':
                        envVarValue = getHomeFolder();
                        break;
                    case 'USERNAME':
                        envVarValue = getUserName();
                        break;
                    case 'HOMEPATH':
                        envVarValue = getHomeFolder();
                        break;
                    case 'TEMP':
                        envVarValue = getTempFolder();
                        break;
                    default:
                        envVarValue = undefined;
                        break;
                }

                if (envVarValue === undefined) {
                    envVarValue = '';
                }
                string = string.replace(envVar, envVarValue);
            }
        }
        return string;
    }

    // TODO: Check that all of these exist
    async prepMo2Config(launcherPath: string, mo2Config: MO2Config, game: PapyrusGame): Promise<MO2Config> {
        let instanceINI = mo2Config.instanceIniPath;
        if (!instanceINI) {
            const { instanceName } = parseMoshortcutURI(mo2Config.shortcutURI);
            const instanceInfo = await FindInstanceForEXE(launcherPath, instanceName);
            if (
                instanceInfo &&
                GetPapyrusGameFromMO2GameID(instanceInfo.gameName) &&
                GetPapyrusGameFromMO2GameID(instanceInfo.gameName) === game
            ) {
                instanceINI = instanceInfo.iniPath;
            }
        } else {
            instanceINI = mo2Config.instanceIniPath
                ? await this.substituteEnvVars(mo2Config.instanceIniPath)
                : mo2Config.instanceIniPath;
        }
        return {
            shortcutURI: mo2Config.shortcutURI,
            profile: mo2Config.profile,
            instanceIniPath: instanceINI,
        } as MO2Config;
    }

    async getConfiguredGameDir(game: PapyrusGame): Promise<string | undefined> {
        let dir = await this._pathResolver.getInstallPath(game);
        return dir || undefined;
    }

    async resolveDebugConfigurationWithSubstitutedVariables(
        _folder: WorkspaceFolder | undefined,
        debugConfiguration: IPapyrusDebugConfiguration,
        _token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration | null | undefined> {
        if (debugConfiguration.request === 'launch') {
            if (debugConfiguration.launchType === 'XSE') {
                if (debugConfiguration.launcherPath === undefined) {
                    // get the configured game directory
                    const installPath = await this.getConfiguredGameDir(debugConfiguration.game);
                    if (installPath === undefined) {
                        throw new Error(
                            'Invalid debug configuration: No "launcherPath" specified and no game configured.'
                        );
                    }
                    let xse = getScriptExtenderExecutableName(debugConfiguration.game);
                    if (!xse) {
                        throw new Error('Invalid debug configuration: No script extender for this game!');
                    }
                    if (debugConfiguration.game === PapyrusGame.starfield) {
                        // special case for starfield; we check if SFSE_loader.exe actually exists in the game dir, and if it doesn't, then we use `Starfield.exe`
                        if (!(await exists(path.join(installPath, xse)))) {
                            xse = 'Starfield.exe';
                        }
                    } else {
                        if (xse === undefined) {
                            throw new Error('Invalid debug configuration: No script extender for this game!');
                        }
                    }
                    debugConfiguration.launcherPath = path.join(installPath, xse);
                }
                return debugConfiguration;
            } else if (debugConfiguration.launchType === 'MO2') {
                // TODO: Auto-find and add launcherPath
                if (!debugConfiguration.launcherPath) {
                    throw new Error('Invalid debug configuration: No "launcherPath" specified for MO2 launcher.');
                }
                const laucherPath = await this.substituteEnvVars(debugConfiguration.launcherPath);
                if (laucherPath === undefined) {
                    throw new Error('Invalid debug configuration.');
                }

                if (debugConfiguration.mo2Config === undefined) {
                    throw new Error('Invalid debug configuration.');
                }
                debugConfiguration.mo2Config = await this.prepMo2Config(
                    laucherPath,
                    debugConfiguration.mo2Config,
                    debugConfiguration.game
                );
                return debugConfiguration;
            }
        }
        // else...
        else if (debugConfiguration.request === 'attach') {
            return debugConfiguration;
        }
        throw new Error('Invalid debug configuration.');
        return undefined;
    }

    dispose() {
        this._registration.dispose();
        this._registrations.map((x) => x.dispose());
    }
}
