import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { CancellationToken, CancellationTokenSource, window } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from "../PapyrusGame";
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { getGameIsRunning, getGamePIDs, mkdirIfNeeded } from '../Utilities';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file';
import { ChildProcess, spawn } from 'node:child_process';
import { timer } from 'rxjs';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);

export enum DebugLaunchState {
    success,
    launcherError,
    gameFailedToStart,
    multipleGamesRunning,
    cancelled,
}
export interface IDebugLauncherService {
    tearDownAfterDebug(): Promise<boolean>;
    runLauncher(
        launcherPath: string,
        launcherArgs: string[],
        game: PapyrusGame,
        cancellationToken?: CancellationToken
    ): Promise<DebugLaunchState>;
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
        launcherPath: string,
        launcherArgs: string[],
        game: PapyrusGame,
        cancellationToken: CancellationToken | undefined
    ): Promise<DebugLaunchState> {
        await this.tearDownAfterDebug();
        if (!cancellationToken) {
            this.cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = this.cancellationTokenSource.token;
        }
        this.currentGame = game;
        this.launcherProcess = spawn(launcherPath, launcherArgs, {
            detached: true,
            stdio: 'ignore',
        });

        // get the current system time
        const GameStartTimeout = 10000;
        let startTime = new Date().getTime();
        // wait for the games process to start
        while (!cancellationToken.isCancellationRequested) {
            if (!(await getGameIsRunning(game)) && (await this.keepSleepingUntil(startTime, GameStartTimeout))) {
                // check if the launcher process failed to launch, or exited and returned an error
                if (
                    !this.launcherProcess ||
                    (this.launcherProcess.exitCode !== null && this.launcherProcess.exitCode !== 0)
                ) {
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
        // both MO2 and the script extender loaders forks and deatches the game process
        let gamePIDs = await getGamePIDs(game);

        if (gamePIDs.length === 0) {
            return DebugLaunchState.gameFailedToStart;
        }

        if (gamePIDs.length > 1) {
            return DebugLaunchState.multipleGamesRunning;
        }
        this.gamePID = gamePIDs[0];

        startTime = new Date().getTime();

        // wait for the game to fully load
        while (!cancellationToken.isCancellationRequested) {
            if (!(await this.keepSleepingUntil(startTime, GameStartTimeout))) {
                break;
            }
        }

        if (cancellationToken.isCancellationRequested) {
            await this.tearDownAfterDebug();
            return DebugLaunchState.cancelled;
        }

        return DebugLaunchState.success;
    }
}

export const IDebugLauncherService: interfaces.ServiceIdentifier<IDebugLauncherService> =
    Symbol('DebugLauncherService');
