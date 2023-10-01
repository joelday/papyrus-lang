import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { CancellationToken, CancellationTokenSource, window } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { getGameIsRunning, getGamePIDs, mkdirIfNeeded } from '../Utilities';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file';
import { ChildProcess, spawn } from 'node:child_process';
import { timer } from 'rxjs';
import { execFile as _execFile } from 'child_process';
const execFile = promisify(_execFile);

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

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
    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
    }

    async tearDownAfterDebug() {
        // If MO2 was already opened by the user before launch, the process would have detached and this will be closed anyway
        if (this.launcherProcess) {
            this.launcherProcess.removeAllListeners();
            this.launcherProcess.kill();
        }
        if (this.gamePID && this.currentGame && (await getGameIsRunning(this.currentGame))) {
            process.kill(this.gamePID);
        }
        this.launcherProcess = undefined;
        this.gamePID = undefined;
        this.currentGame = undefined;
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
        let cmd = launcherCommand.command;
        let args = launcherCommand.args;
        let _stdOut: string = '';
        let _stdErr: string = '';
        this.launcherProcess = spawn(cmd, args);
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
        const GameStartTimeout = 15000;
        // get the current system time
        let startTime = new Date().getTime();
        // wait for the games process to start
        while (!cancellationToken.isCancellationRequested) {
            if (!(await getGameIsRunning(game)) && (await this.keepSleepingUntil(startTime, GameStartTimeout))) {
                // check if the launcher process failed to launch, or exited and returned an error
                if (
                    !this.launcherProcess ||
                    (this.launcherProcess.exitCode !== null && this.launcherProcess.exitCode !== 0)
                ) {
                    window.showErrorMessage(
                        `Launcher process exited with error code ${
                            this.launcherProcess.exitCode
                        }.\ncmd: ${cmd}\nargs: ${args.join(' ')}\nstderr: ${_stdErr}\nstdout: ${_stdOut}`
                    );
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
        let gamePIDs = await getGamePIDs(game);

        if (gamePIDs.length === 0) {
            return DebugLaunchState.gameFailedToStart;
        }

        if (gamePIDs.length > 1) {
            return DebugLaunchState.multipleGamesRunning;
        }
        this.gamePID = gamePIDs[0];

        // TODO: REMOVE THIS SHIT WHEN WE YEET THE DEBUGADAPTERPROXY
        startTime = new Date().getTime();

        // wait for the game to fully load
        let waitedForGame = false;
        while (!cancellationToken.isCancellationRequested) {
            if (await this.keepSleepingUntil(startTime, GameStartTimeout)) {
                if (!(await getGameIsRunning(game))) {
                    return DebugLaunchState.gameExitedBeforeOpening;
                }
            } else {
                waitedForGame = true;
                break;
            }
        }
        if (!waitedForGame && cancellationToken.isCancellationRequested) {
            await this.tearDownAfterDebug();
            return DebugLaunchState.cancelled;
        }

        return DebugLaunchState.success;
    }
}

export const IDebugLauncherService: interfaces.ServiceIdentifier<IDebugLauncherService> =
    Symbol('DebugLauncherService');
