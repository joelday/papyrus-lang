// TODO: Remove, no longer necessary

import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { take } from 'rxjs/operators';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame, getGameIniName } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { ClientHostStatus } from '../server/LanguageClientHost';
import { CheckIfDebuggingIsEnabledInIni, TurnOnDebuggingInIni } from '../common/GameHelpers';
import { WriteChangesToIni, ParseIniFile } from '../common/INIHelpers';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

const exists = promisify(fs.exists);

export enum GameDebugConfigurationState {
    debugEnabled,
    debugNotEnabled,
    gameIniMissing,
    gameUserDirMissing,
    gameMissing,
    gameDisabled,
}

export interface IGameDebugConfiguratorService {
    getState(game: PapyrusGame, gameUserDir?: string): Promise<GameDebugConfigurationState>;
    configureDebug(game: PapyrusGame, gameUserDir?: string): Promise<boolean>;
}
@injectable()
export class GameDebugConfiguratorService implements IGameDebugConfiguratorService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _pathResolver: IPathResolver;

    constructor(
        @inject(ILanguageClientManager) languageClientManager: ILanguageClientManager,
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
    }

    async getState(game: PapyrusGame, gameUserDir: string | undefined): Promise<GameDebugConfigurationState> {
        const client = await this._languageClientManager.getLanguageClientHost(game);
        const status = await client.status.pipe(take(1)).toPromise();
        if (!gameUserDir) {
            if (status === ClientHostStatus.disabled) {
                return GameDebugConfigurationState.gameDisabled;
            }
            if (status === ClientHostStatus.missing) {
                return GameDebugConfigurationState.gameMissing;
            }
        }
        const gameUserDirPath = gameUserDir || (await this._pathResolver.getUserGamePath(game));
        if (!gameUserDirPath) {
            return GameDebugConfigurationState.gameUserDirMissing;
        }
        const gameIniPath = path.join(gameUserDirPath, getGameIniName(game));
        if (!(await exists(gameIniPath))) {
            return GameDebugConfigurationState.gameIniMissing;
        }
        const inidata = await ParseIniFile(gameIniPath);
        if (!inidata) {
            return GameDebugConfigurationState.gameIniMissing;
        }
        if (!CheckIfDebuggingIsEnabledInIni(game, inidata)) {
            return GameDebugConfigurationState.debugNotEnabled;
        }
        return GameDebugConfigurationState.debugEnabled;
    }

    async configureDebug(game: PapyrusGame, gameUserDir: string | undefined): Promise<boolean> {
        const gameUserDirPath = gameUserDir || (await this._pathResolver.getUserGamePath(game));
        if (!gameUserDirPath) {
            return false;
        }
        const gameIniPath = path.join(gameUserDirPath, getGameIniName(game));
        if (!(await exists(gameIniPath))) {
            return false;
        }
        const inidata = await ParseIniFile(gameIniPath);
        if (!inidata) {
            return false;
        }
        const newinidata = TurnOnDebuggingInIni(game, inidata);
        return await WriteChangesToIni(gameIniPath, newinidata);
    }
}

export const IGameDebugConfiguratorService: interfaces.ServiceIdentifier<IGameDebugConfiguratorService> =
    Symbol('GameDebugConfiguratorService');
