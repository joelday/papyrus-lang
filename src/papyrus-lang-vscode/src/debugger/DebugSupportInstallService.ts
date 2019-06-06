import { createDecorator } from 'decoration-ioc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, CancellationToken, Progress, CancellationTokenSource } from 'vscode';
import { take } from 'rxjs/operators';
import { resolveInstallPath } from '../Utilities';
import { PapyrusGame, getScriptExtenderName } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { ClientHostStatus } from '../server/LanguageClientHost';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file/promise';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);

const bundledPluginPath = 'debug-plugin';

function getExtenderPluginPath(game: PapyrusGame) {
    return `Data/${getScriptExtenderName(game)}/Plugins`;
}

function getPluginDllName(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'DarkId.Papyrus.DebugServer.dll';
        case PapyrusGame.skyrimSpecialEdition:
            return 'DarkId.Papyrus.DebugServer.Skyrim.dll';
        default:
            throw new Error(`'${game}' is not supported by the Papyrus debugger.`);
    }
}

export enum DebugSupportInstallState {
    notInstalled,
    installed,
    incorrectVersion,
    gameMissing,
    gameDisabled,
}

export interface IDebugSupportInstallService {
    getInstallState(game: PapyrusGame): Promise<DebugSupportInstallState>;
    installPlugin(game: PapyrusGame, cancellationToken?: CancellationToken): Promise<boolean>;
}

export class DebugSupportInstallService implements IDebugSupportInstallService {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;
    private readonly _languageClientManager: ILanguageClientManager;

    constructor(
        @ILanguageClientManager languageClientManager: ILanguageClientManager,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._context = context;
    }

    private async getPluginInstallPath(game: PapyrusGame) {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())[game];
        const resolvedInstallPath = await resolveInstallPath(PapyrusGame.fallout4, config.installPath, this._context);

        if (!resolvedInstallPath) {
            return null;
        }

        return path.join(resolvedInstallPath, getExtenderPluginPath(game), getPluginDllName(game));
    }

    private getBundledPluginPath(game: PapyrusGame) {
        return this._context.asAbsolutePath(path.join(bundledPluginPath, getPluginDllName(game)));
    }

    async getInstallState(game: PapyrusGame): Promise<DebugSupportInstallState> {
        const client = await this._languageClientManager.getLanguageClientHost(game);
        const status = await client.status.pipe(take(1)).toPromise();

        if (status === ClientHostStatus.disabled) {
            return DebugSupportInstallState.gameDisabled;
        }

        if (status === ClientHostStatus.missing) {
            return DebugSupportInstallState.gameMissing;
        }

        const installedPluginPath = await this.getPluginInstallPath(game);

        if (!(await exists(installedPluginPath))) {
            return DebugSupportInstallState.notInstalled;
        }

        const installedHash = await md5File(installedPluginPath);
        const bundledHash = await md5File(this.getBundledPluginPath(game));

        if (installedHash !== bundledHash) {
            return DebugSupportInstallState.incorrectVersion;
        }

        return DebugSupportInstallState.installed;
    }

    async installPlugin(game: PapyrusGame, cancellationToken = new CancellationTokenSource().token): Promise<boolean> {
        const pluginInstallPath = await this.getPluginInstallPath(game);
        const bundledPluginPath = await this.getBundledPluginPath(game);

        if (cancellationToken.isCancellationRequested) {
            return false;
        }

        await copyFile(bundledPluginPath, pluginInstallPath);

        return true;
    }
}

export const IDebugSupportInstallService = createDecorator<IDebugSupportInstallService>('debugSupportInstallService');
