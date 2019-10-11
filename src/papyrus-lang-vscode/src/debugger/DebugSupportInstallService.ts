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
const removeFile = promisify(fs.unlink);

const bundledPluginPath = 'debug-plugin';

function getExtenderPluginPath(game: PapyrusGame) {
    return `Data/${getScriptExtenderName(game)}/Plugins`;
}

function getPluginDllName(game: PapyrusGame, legacy = false) {
    switch (game) {
        case PapyrusGame.fallout4:
            return legacy ? 'DarkId.Papyrus.DebugServer.dll' : 'DarkId.Papyrus.DebugServer.Fallout4.dll';
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

    private async getPluginInstallPath(game: PapyrusGame, legacy = false) {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())[game];
        const resolvedInstallPath = await resolveInstallPath(game, config.installPath, this._context);

        if (!resolvedInstallPath) {
            return null;
        }

        return path.join(resolvedInstallPath, getExtenderPluginPath(game), getPluginDllName(game, legacy));
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

        // If the debugger plugin isn't bundled, we'll assume this is in-development.
        if (!(await exists(this.getBundledPluginPath(game)))) {
            return DebugSupportInstallState.installed;
        }

        // For clarity and consistency, the plugin is being renamed to end with Fallout4.dll
        // This handles the case where the old version is installed.
        const legacyInstalledPluginPath = await this.getPluginInstallPath(game, true);
        if (game === PapyrusGame.fallout4 && (await exists(legacyInstalledPluginPath))) {
            return DebugSupportInstallState.incorrectVersion;
        }

        const installedPluginPath = await this.getPluginInstallPath(game, false);
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
        // Remove the legacy dll if it exists.
        const legacyInstalledPluginPath = await this.getPluginInstallPath(game, true);
        if (game === PapyrusGame.fallout4 && (await exists(legacyInstalledPluginPath))) {
            await removeFile(legacyInstalledPluginPath);
        }

        const pluginInstallPath = await this.getPluginInstallPath(game, false);
        const bundledPluginPath = await this.getBundledPluginPath(game);

        if (cancellationToken.isCancellationRequested) {
            return false;
        }

        await copyFile(bundledPluginPath, pluginInstallPath);

        return true;
    }
}

export const IDebugSupportInstallService = createDecorator<IDebugSupportInstallService>('debugSupportInstallService');
