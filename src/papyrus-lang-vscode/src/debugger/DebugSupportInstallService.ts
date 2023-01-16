import { inject, injectable, interfaces } from 'inversify';
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

import md5File from 'md5-file';

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

@injectable()
export class DebugSupportInstallService implements IDebugSupportInstallService {
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
        // TODO: Figure out if this is how it should still be done. Can figure that out once we start doing release
        // builds.
        if (!(await exists(bundledPluginPath))) {
            return DebugSupportInstallState.installed;
        }

        const installedPluginPath = await this._pathResolver.getDebugPluginInstallPath(game, false);
        if (!installedPluginPath || !(await exists(installedPluginPath))) {
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
        const pluginInstallPath = await this._pathResolver.getDebugPluginInstallPath(game, false);
        if (!pluginInstallPath) {
            return false;
        }

        const bundledPluginPath = await this._pathResolver.getDebugPluginBundledPath(game);

        if (cancellationToken.isCancellationRequested) {
            return false;
        }

        await mkdirIfNeeded(path.dirname(pluginInstallPath));
        await copyFile(bundledPluginPath, pluginInstallPath);

        return true;
    }
}

export const IDebugSupportInstallService: interfaces.ServiceIdentifier<IDebugSupportInstallService> = Symbol('debugSupportInstallService');
