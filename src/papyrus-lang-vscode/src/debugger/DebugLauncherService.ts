import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { CancellationToken, CancellationTokenSource, window } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { getGameIsRunning, getGamePIDs } from '../Utilities';
import { ChildProcess, spawn } from 'node:child_process';
import waitPort from 'wait-port';

export enum DebugLaunchState {
    success,
    launcherError,
    gameFailedToStart,
    gameExitedBeforeOpening,
    multipleGamesRunning,
    cancelled,
}
export interface IDebugLauncherService {
    tearDownAfterDebug(): Promise<boolean>;
    runLauncher(
        launcherCommand: LaunchCommand,
        game: PapyrusGame,
        portToCheck: number,
        cancellationToken?: CancellationToken
    ): Promise<DebugLaunchState>;
}

export interface LaunchCommand {
    command: string;
    args: string[];
    cwd?: string;
}

@injectable()
export class DebugLauncherService implements IDebugLauncherService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _pathResolver: IPathResolver;

    // TODO: Move this stuff into the global Context
    private cancellationTokenSource: CancellationTokenSource | undefined;
    private launcherProcess: ChildProcess | undefined;
    private gamePID: number | undefined;
    private currentGame: PapyrusGame | undefined;
    private tearingDown = false;
    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
    }
    private reset() {
        this.launcherProcess = undefined;
        this.gamePID = undefined;
        this.currentGame = undefined;
    }

    async tearDownAfterDebug() {
        // If MO2 was already opened by the user before launch, the process would have detached and this will be closed anyway
        if (this.launcherProcess) {
            this.launcherProcess.removeAllListeners();
            try {
                if (this.launcherProcess.kill()) {
                    this.reset();
                } else if (this.launcherProcess.kill('SIGKILL')) {
                    this.reset();
                }
            } catch (e) {
                console.error(e);
            }
        }

        if (this.gamePID && this.currentGame && (await getGameIsRunning(this.currentGame))) {
            try {
                process.kill(this.gamePID);
            } catch (e) {
                /* empty */
            }
        }

        this.reset();

        return true;
    }

    async keepSleepingUntil(startTime: number, timeout: number) {
        const currentTime = new Date().getTime();

        if (currentTime > startTime + timeout) {
            return false;
        }
        await new Promise((resolve) => setTimeout(resolve, 200));
        return true;
    }

    async cancelLaunch() {
        if (this.cancellationTokenSource) {
            this.cancellationTokenSource.cancel();
        }
    }

    async runLauncher(
        launcherCommand: LaunchCommand,
        game: PapyrusGame,
        portToCheck: number,
        cancellationToken: CancellationToken | undefined
    ): Promise<DebugLaunchState> {
        await this.tearDownAfterDebug();
        if (!cancellationToken) {
            this.cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = this.cancellationTokenSource.token;
        }
        this.currentGame = game;
        const cmd = launcherCommand.command;
        const args = launcherCommand.args;
        const port = portToCheck;
        let _stdOut: string = '';
        let _stdErr: string = '';
        this.launcherProcess = spawn(cmd, args, {
            cwd: launcherCommand.cwd,
        });
        if (!this.launcherProcess || !this.launcherProcess.stdout || !this.launcherProcess.stderr) {
            window.showErrorMessage(`Failed to start launcher process.\ncmd: ${cmd}\nargs: ${args.join(' ')}`);
            return DebugLaunchState.launcherError;
        }
        this.launcherProcess.stdout.on('data', (data) => {
            _stdOut += data;
        });
        this.launcherProcess.stderr.on('data', (data) => {
            _stdErr += data;
        });
        const _handleProcessExit = () => {
            return (
                !this.launcherProcess || (this.launcherProcess.exitCode !== null && this.launcherProcess.exitCode !== 0)
            );
        };
        const _showErrorCode = () => {
            window.showErrorMessage(
                `Launcher process exited with error code ${
                    this.launcherProcess?.exitCode || -1
                }.\ncmd: ${cmd}\nargs: ${args.join(' ')}\nstderr: ${_stdErr}\nstdout: ${_stdOut}`
            );
        };
        const GameStartTimeout = 15000;
        // get the current system time
        let startTime = new Date().getTime();
        // wait for the games process to start
        while (!cancellationToken.isCancellationRequested) {
            const gameIsRunning = await getGameIsRunning(game);
            const timedOut = !(await this.keepSleepingUntil(startTime, GameStartTimeout));
            if (!gameIsRunning && !timedOut) {
                // check if the launcher process failed to launch, or exited and returned an error
                if (_handleProcessExit()) {
                    _showErrorCode();
                    return DebugLaunchState.launcherError;
                }
            } else {
                break;
            }
        }

        if (cancellationToken.isCancellationRequested) {
            await this.tearDownAfterDebug();
            return DebugLaunchState.cancelled;
        }
        // we can't get the PID of the game from the launcher process because
        // both MO2 and the script extender loaders fork and deatch the game process
        const gamePIDs = await getGamePIDs(game);

        if (gamePIDs.length === 0) {
            return DebugLaunchState.gameFailedToStart;
        }

        if (gamePIDs.length > 1) {
            return DebugLaunchState.multipleGamesRunning;
        }
        this.gamePID = gamePIDs[0];

        // game has launched, now we wait for the port to open
        const connectionTimeout = 15000;
        startTime = new Date().getTime();
        // TODO: Remember to check for starfield only when we remove the skyrim/fallout 4 proxy
        let result = false;
        while (!cancellationToken.isCancellationRequested) {
            const gameIsRunning = await getGameIsRunning(game);
            const currentTime = new Date().getTime();
            const timedOut = currentTime > startTime + connectionTimeout;
            if (timedOut) {
                window.showErrorMessage(`Debugger failed to connect.`);
                return DebugLaunchState.gameFailedToStart;
            } else if (!gameIsRunning) {
                if (_handleProcessExit()) {
                    _showErrorCode();
                    return DebugLaunchState.launcherError;
                }
                return DebugLaunchState.gameFailedToStart;
            } else {
                result = (
                    await waitPort({
                        host: 'localhost',
                        port: port,
                        timeout: 1000,
                        interval: 1000,
                        output: 'silent',
                    })
                ).open;
                if (result) {
                    break;
                }
            }
        }
        return result ? DebugLaunchState.success : DebugLaunchState.gameFailedToStart;
    }
}

export const IDebugLauncherService: interfaces.ServiceIdentifier<IDebugLauncherService> =
    Symbol('DebugLauncherService');
