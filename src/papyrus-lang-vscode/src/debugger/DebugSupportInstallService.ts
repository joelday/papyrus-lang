import { createDecorator } from 'decoration-ioc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { CancellationToken, CancellationTokenSource } from 'vscode';
import { take } from 'rxjs/operators';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { ClientHostStatus } from '../server/LanguageClientHost';
import { mkdirIfNeeded } from '../Utilities';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file/promise';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);
const removeFile = promisify(fs.unlink);


export enum DebugSupportInstallState {
    notInstalled,
    installed,
    installedAsMod,
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
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _pathResolver: IPathResolver;

    constructor(
        @ILanguageClientManager languageClientManager: ILanguageClientManager,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IPathResolver pathResolver: IPathResolver
    ) {
        this._languageClientManager = languageClientManager;
        this._configProvider = configProvider;
        this._pathResolver = pathResolver;
    }

    async getInstallState(game: PapyrusGame): Promise<DebugSupportInstallState> {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())[game];
        const client = await this._languageClientManager.getLanguageClientHost(game);
        const status = await client.status.pipe(take(1)).toPromise();

        if (status === ClientHostStatus.disabled) {
            return DebugSupportInstallState.gameDisabled;
        }

        if (status === ClientHostStatus.missing) {
            return DebugSupportInstallState.gameMissing;
        }

        const bundledPluginPath = await this._pathResolver.getDebugPluginBundledPath(game);
        // If the debugger plugin isn't bundled, we'll assume this is in-development.
        if (!(await exists(bundledPluginPath))) {
            return DebugSupportInstallState.installed;
        }

        // For clarity and consistency, the plugin is being renamed to end with Fallout4.dll
        // This handles the case where the old version is installed.
        const legacyInstalledPluginPath = await this._pathResolver.getDebugPluginInstallPath(game, true);
        if (game === PapyrusGame.fallout4 && (await exists(legacyInstalledPluginPath))) {
            return DebugSupportInstallState.incorrectVersion;
        }

        const installedPluginPath = await this._pathResolver.getDebugPluginInstallPath(game, false);
        if (!(await exists(installedPluginPath))) {
            return DebugSupportInstallState.notInstalled;
        }

        const installedHash = await md5File(installedPluginPath);
        const bundledHash = await md5File(await this._pathResolver.getDebugPluginBundledPath(game));

        if (installedHash !== bundledHash) {
            return DebugSupportInstallState.incorrectVersion;
        }

        if (config.modDirectoryPath) {
            return DebugSupportInstallState.installedAsMod;
        }

        return DebugSupportInstallState.installed;
    }

    async installPlugin(game: PapyrusGame, cancellationToken = new CancellationTokenSource().token): Promise<boolean> {
        // Remove the legacy dll if it exists.
        const legacyInstalledPluginPath = await this._pathResolver.getDebugPluginInstallPath(game, true);
        if (game === PapyrusGame.fallout4 && (await exists(legacyInstalledPluginPath))) {
            await removeFile(legacyInstalledPluginPath);
        }

        const pluginInstallPath = await this._pathResolver.getDebugPluginInstallPath(game, false);
        const bundledPluginPath = await this._pathResolver.getDebugPluginBundledPath(game);

        if (cancellationToken.isCancellationRequested) {
            return false;
        }

        mkdirIfNeeded(path.dirname(pluginInstallPath));
        await copyFile(bundledPluginPath, pluginInstallPath);
        return true;
    }
}

export const IDebugSupportInstallService = createDecorator<IDebugSupportInstallService>('debugSupportInstallService');
