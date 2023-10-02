import {
    DebugAdapterDescriptorFactory,
    DebugAdapterExecutable,
    DebugAdapterDescriptor,
    debug,
    Disposable,
    window,
    commands,
    Uri,
    env,
    CancellationTokenSource,
} from 'vscode';
import {
    PapyrusGame,
    getDisplayNameForGame,
    getScriptExtenderName,
    getScriptExtenderUrl,
    getShortDisplayNameForGame,
} from '../PapyrusGame';
import { ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { take } from 'rxjs/operators';
import { IPapyrusDebugSession } from './PapyrusDebugSession';
import { toCommandLineArgs, getGameIsRunning } from '../Utilities';
import { IPathResolver } from '../common/PathResolver';
import { IDebugSupportInstallService, DebugSupportInstallState } from './DebugSupportInstallService';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { showGameDisabledMessage, showGameMissingMessage } from '../features/commands/InstallDebugSupportCommand';
import { inject, injectable } from 'inversify';
import { DebugLaunchState, IDebugLauncherService, LaunchCommand } from './DebugLauncherService';
import { IMO2LauncherDescriptor, IMO2LaunchDescriptorFactory } from './MO2LaunchDescriptorFactory';
import {
    GetErrorMessageFromStatus,
    IMO2ConfiguratorService,
    MO2LaunchConfigurationStatus,
} from './MO2ConfiguratorService';
import path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';
import { isMO2ButNotThisOneRunning, killAllMO2Processes } from './MO2Helpers';
const exists = promisify(fs.exists);

const noopExecutable = new DebugAdapterExecutable('node', ['-e', '""']);

export interface IDebugToolArguments {
    port?: number;
    projectPath?: string;
    defaultScriptSourceFolder?: string;
    defaultAdditionalImports?: string;
    creationKitInstallPath: string;
    relativeIniPaths: string[];
    clientProcessId: number;
}

function getDefaultPortForGame(game: PapyrusGame) {
    return game === PapyrusGame.fallout4 ? 2077 : 43201;
}

@injectable()
export class PapyrusDebugAdapterDescriptorFactory implements DebugAdapterDescriptorFactory {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _pathResolver: IPathResolver;
    private readonly _debugSupportInstaller: IDebugSupportInstallService;
    private readonly _debugLauncher: IDebugLauncherService;
    private readonly _MO2LaunchDescriptorFactory: IMO2LaunchDescriptorFactory;
    private readonly _MO2ConfiguratorService: IMO2ConfiguratorService;
    private readonly _registration: Disposable;

    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(ICreationKitInfoProvider) creationKitInfoProvider: ICreationKitInfoProvider,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver,
        @inject(IDebugSupportInstallService) debugSupportInstaller: IDebugSupportInstallService,
        @inject(IDebugLauncherService) debugLauncher: IDebugLauncherService,
        @inject(IMO2LaunchDescriptorFactory) mo2LaunchDescriptorFactory: IMO2LaunchDescriptorFactory,
        @inject(IMO2ConfiguratorService) mo2ConfiguratorService: IMO2ConfiguratorService
    ) {
        this._languageClientManager = languageClientManager;
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
        this._debugSupportInstaller = debugSupportInstaller;
        this._debugLauncher = debugLauncher;
        this._MO2LaunchDescriptorFactory = mo2LaunchDescriptorFactory;
        this._MO2ConfiguratorService = mo2ConfiguratorService;
        this._registration = debug.registerDebugAdapterDescriptorFactory('papyrus', this);
    }

    private async _ShowAttachDebugSupportInstallMessage(game: PapyrusGame) {
        const getExtenderOption = `Get ${getScriptExtenderName(game)}`;
        const installOption = `Install ${getScriptExtenderName(game)} Plugin`;

        const selectedInstallOption = await window.showInformationMessage(
            `Papyrus debugging support requires a plugin for ${getDisplayNameForGame(
                game
            )} Script Extender (${getScriptExtenderName(
                game
            )}) to be installed. After installation has completed, launch ${getShortDisplayNameForGame(
                game
            )} with ${getScriptExtenderName(game)} and wait until the main menu has loaded.`,
            getExtenderOption,
            installOption,
            'Cancel'
        );

        switch (selectedInstallOption) {
            case installOption:
                commands.executeCommand(`papyrus.${game}.installDebuggerSupport`);
                break;
            case getExtenderOption:
                env.openExternal(Uri.parse(getScriptExtenderUrl(game)));
                break;
        }
        return false;
    }

    private async _ShowLaunchDebugSupportInstallMessage(
        game: PapyrusGame,
        launchType: 'MO2' | 'XSE',
        launcher: IMO2LauncherDescriptor
    ) {
        const installOption = `Fix Configuration`;
        const state = await this._MO2ConfiguratorService.getStateFromConfig(launcher);
        if (state !== MO2LaunchConfigurationStatus.Ready) {
            const errorMessage = GetErrorMessageFromStatus(state);
            const selectedInstallOption = await window.showInformationMessage(
                `The following configuration problems were encountered while attempting to launch ${getDisplayNameForGame(
                    game
                )}:\n${errorMessage}\nWould you like to fix the configuration?`,
                installOption,
                'Cancel'
            );
            switch (selectedInstallOption) {
                case installOption:
                    if (launchType === 'MO2') {
                        commands.executeCommand(`papyrus.${game}.installDebuggerSupport`, [launchType, launcher]);
                    } else {
                        commands.executeCommand(`papyrus.${game}.installDebuggerSupport`, [launchType]);
                    }
                    break;
                case 'Cancel':
                    return true;
            }
        }
        return false;
    }

    private async _attachEnsureGameInstalled(game: PapyrusGame, modsDir?: string) {
        const installState = await this._debugSupportInstaller.getInstallState(game, modsDir);

        switch (installState) {
            case DebugSupportInstallState.incorrectVersion: {
                const ignoreVersion = (await this._configProvider.config.pipe(take(1)).toPromise())[game]
                    .ignoreDebuggerVersion;

                if (ignoreVersion) {
                    break;
                }

                const selectedUpdateOption = await window.showWarningMessage(
                    `The Papyrus debugging support ${getScriptExtenderName(game)} plugin needs to be updated.`,
                    'Update',
                    'Remind Me Later',
                    'Cancel'
                );

                if (selectedUpdateOption === 'Update') {
                    commands.executeCommand(`papyrus.${game}.installDebuggerSupport`);
                    return false;
                }

                if (selectedUpdateOption === 'Cancel' || selectedUpdateOption === undefined) {
                    return false;
                }
                break;
            }

            case DebugSupportInstallState.notInstalled:
                return await this._ShowAttachDebugSupportInstallMessage(game);
            case DebugSupportInstallState.gameDisabled:
                showGameDisabledMessage(game);
                return false;
            case DebugSupportInstallState.gameMissing:
                showGameMissingMessage(game);
                return false;
        }
        return true;
    }

    async ensureGameRunning(game: PapyrusGame) {
        if (!(await getGameIsRunning(game))) {
            const selectedGameRunningOption = await window.showWarningMessage(
                `Make sure that ${getDisplayNameForGame(game)} is running and is either in-game or at the main menu.`,
                'Continue',
                'Cancel'
            );

            if (selectedGameRunningOption !== 'Continue') {
                return false;
            }
        }

        return true;
    }

    async createDebugAdapterDescriptor(
        session: IPapyrusDebugSession,
        _executable: DebugAdapterExecutable
    ): Promise<DebugAdapterDescriptor> {
        const game = session.configuration.game;

        if (game !== PapyrusGame.fallout4 && game !== PapyrusGame.skyrimSpecialEdition) {
            throw new Error(`'${game}' is not supported by the Papyrus debugger.`);
        }
        let launched = DebugLaunchState.success;

        if (session.configuration.request === 'launch') {
            // check if the game is running
            if (await getGameIsRunning(game)) {
                throw new Error(
                    `'${getDisplayNameForGame(
                        game
                    )}' is already running. Please close it before launching the debugger.`
                );
            }
            // run the launcher with the args from the configuration
            // if the launcher is MO2
            let launcherPath: string = session.configuration.launcherPath || '';
            if (!launcherPath) {
                throw new Error(`'Invalid launch configuration. Launcher path is missing.`);
            }
            launcherPath = path.normalize(launcherPath);
            if (!launcherPath || !(await exists(launcherPath))) {
                throw new Error(`'Path does not exist!`);
            }
            const launcherArgs: string[] = session.configuration.args || [];
            let LauncherCommand: LaunchCommand;
            if (session.configuration.launchType === 'MO2') {
                if (session.configuration.mo2Config === undefined) {
                    throw new Error(`'Invalid launch configuration. MO2 configuration is missing.`);
                }
                const launcher = await this._MO2LaunchDescriptorFactory.createMO2LaunchDecriptor(
                    launcherPath,
                    launcherArgs,
                    session.configuration.mo2Config,
                    game
                );
                const state = await this._MO2ConfiguratorService.getStateFromConfig(launcher);
                if (state !== MO2LaunchConfigurationStatus.Ready) {
                    if (!(await this._ShowLaunchDebugSupportInstallMessage(game, 'MO2', launcher))) {
                        session.configuration.noop = true;
                        return noopExecutable;
                    }
                }

                // Configuration is ready, get the launch command
                LauncherCommand = launcher.getLaunchCommand();

                // If MO2 is running and the profile is not the one we want to launch, the launch will fuck up, kill it
                if (
                    (await isMO2ButNotThisOneRunning(launcher.MO2EXEPath)) ||
                    launcher.instanceInfo.selectedProfile !== launcher.profileToLaunchData.name
                ) {
                    await killAllMO2Processes();
                }
            } else if (session.configuration.launchType === 'XSE') {
                LauncherCommand = { command: launcherPath, args: launcherArgs };
            } else {
                // throw an error indicated the launch configuration is invalid
                throw new Error(`'Invalid launch configuration.`);
            }

            const cancellationSource = new CancellationTokenSource();
            const cancellationToken = cancellationSource.token;
            const port = session.configuration.port || getDefaultPortForGame(game);
            const wait_message = window.setStatusBarMessage(
                `Waiting for ${getDisplayNameForGame(game)} to start...`,
                30000
            );
            launched = await this._debugLauncher.runLauncher(LauncherCommand, game, port, cancellationToken);
            wait_message.dispose();
        } else {
            if (!(await this._attachEnsureGameInstalled(game))) {
                session.configuration.noop = true;
                return noopExecutable;
            }
        }

        if (launched != DebugLaunchState.success) {
            if (launched === DebugLaunchState.cancelled) {
                session.configuration.noop = true;
                return noopExecutable;
            }
            if (launched === DebugLaunchState.multipleGamesRunning) {
                const errMessage = `Multiple ${getDisplayNameForGame(
                    game
                )} instances are running, shut them down and try again.`;
                window.showErrorMessage(errMessage);
            }
            // throw an error indicating the launch failed
            throw new Error(`'${game}' failed to launch.`);
            // attach
        } else if (!(await this.ensureGameRunning(game))) {
            session.configuration.noop = true;
            return noopExecutable;
        }
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())[game];
        const creationKitInfo = await this._creationKitInfoProvider.infos.get(game)!.pipe(take(1)).toPromise();

        if (!creationKitInfo.resolvedInstallPath) {
            throw new Error(`Creation Kit install path for ${getDisplayNameForGame(game)} is not configured.`);
        }

        const toolArguments: IDebugToolArguments = {
            port: session.configuration.port || getDefaultPortForGame(game),
            projectPath: session.configuration.projectPath,
            creationKitInstallPath: creationKitInfo.resolvedInstallPath,
            relativeIniPaths: config.creationKitIniFiles,
            defaultScriptSourceFolder: creationKitInfo.config.Papyrus?.sScriptSourceFolder,
            defaultAdditionalImports: creationKitInfo.config.Papyrus?.sAdditionalImports,
            clientProcessId: Number.parseInt(process.env.VSCODE_PID!),
        };

        const toolPath = await this._pathResolver.getDebugToolPath(game);
        const commandLineArgs = toCommandLineArgs(toolArguments);

        const outputChannel = (await this._languageClientManager.getLanguageClientHost(session.configuration.game))
            .outputChannel;
        outputChannel?.appendLine(
            `Debug session: Launching debug adapter client: ${toolPath} ${commandLineArgs.join(' ')}`
        );

        return new DebugAdapterExecutable(toolPath, commandLineArgs);
    }

    dispose() {
        this._registration.dispose();
    }
}
