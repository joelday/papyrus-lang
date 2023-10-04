// TODO: Remove, no longer necessary

import { inject, injectable, interfaces } from 'inversify';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { CheckGameConfigForDebug, CheckValidGameUserDir, ConfigureDebug } from '../common/GameHelpers';

export enum GameDebugConfigurationState {
    debugEnabled,
    debugNotEnabled,
    gameUserDirInvalid,
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
        // We no longer need to check debugging settings in the ini for Skyrim or Fallout 4
        // The debugger plugin takes care of that for us
        if (game === PapyrusGame.skyrim || game === PapyrusGame.fallout4) {
            return GameDebugConfigurationState.debugEnabled;
        }
        const gameUserDirPath = gameUserDir || (await this._pathResolver.getUserGamePath(game));
        if (!gameUserDirPath) {
            return GameDebugConfigurationState.gameUserDirMissing;
        }

        if (!(await CheckValidGameUserDir(game, gameUserDirPath))) {
            return GameDebugConfigurationState.gameUserDirInvalid;
        }

        const check = await CheckGameConfigForDebug(game, gameUserDirPath);
        return check ? GameDebugConfigurationState.debugEnabled : GameDebugConfigurationState.debugNotEnabled;
    }

    async configureDebug(game: PapyrusGame, gameUserDir: string | undefined): Promise<boolean> {
        const gameUserDirPath = gameUserDir || (await this._pathResolver.getUserGamePath(game));
        return await ConfigureDebug(game, gameUserDirPath!);
    }
}

export const IGameDebugConfiguratorService: interfaces.ServiceIdentifier<IGameDebugConfiguratorService> =
    Symbol('GameDebugConfiguratorService');
