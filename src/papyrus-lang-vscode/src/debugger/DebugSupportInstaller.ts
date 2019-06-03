import { createDecorator } from 'decoration-ioc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, CancellationToken, Progress } from 'vscode';
import { take } from 'rxjs/operators';
import { resolveInstallPath } from '../Utilities';
import { PapyrusGame } from '../PapyrusGame';
import { LanguageClientConsumerBase } from '../common/LanguageClientConsumerBase';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { ClientHostStatus } from '../server/LanguageClientHost';

import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

import md5File from 'md5-file/promise';

const exists = promisify(fs.exists);
const copyFile = promisify(fs.copyFile);

const pluginPath = 'Data/F4SE/Plugins';
const pluginDllName = 'DarkId.Papyrus.DebugServer.dll';
const bundledPluginPath = 'debug-plugin';

export enum DebugSupportInstallState {
    missing,
    installed,
    incorrectVersion,
    gameMissing,
    gameDisabled,
    cancelled,
}

export interface IDebugSupportInstaller {
    getInstallState(
        progress?: Progress<{ increment: number; message: string }>,
        token?: CancellationToken
    ): Promise<DebugSupportInstallState>;
    installPlugin(
        progress?: Progress<{ increment: number; message: string }>,
        token?: CancellationToken
    ): Promise<void>;
}

export class DebugSupportInstaller extends LanguageClientConsumerBase implements IDebugSupportInstaller {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;

    constructor(
        @ILanguageClientManager languageClientManager: ILanguageClientManager,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        super(languageClientManager, PapyrusGame.fallout4);

        this._configProvider = configProvider;
        this._context = context;
    }

    private async getPluginInstallPath() {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise()).fallout4;
        const resolvedInstallPath = await resolveInstallPath(PapyrusGame.fallout4, config.installPath, this._context);

        if (!resolvedInstallPath) {
            return null;
        }

        return path.join(resolvedInstallPath, pluginPath, pluginDllName);
    }

    private getBundledPluginPath() {
        return this._context.asAbsolutePath(path.join(bundledPluginPath, pluginDllName));
    }

    async getInstallState(
        progress?: Progress<{ increment: number; message: string }>,
        token?: CancellationToken
    ): Promise<DebugSupportInstallState> {
        if (progress) {
            progress.report({ increment: 10, message: 'Waiting for language client...' });
        }

        const client = await this.getLanguageClientHost();
        const status = await client.status.pipe(take(1)).toPromise();

        if (status === ClientHostStatus.disabled) {
            return DebugSupportInstallState.gameDisabled;
        }

        if (status === ClientHostStatus.missing) {
            return DebugSupportInstallState.gameMissing;
        }

        if (token && token.isCancellationRequested) {
            return DebugSupportInstallState.cancelled;
        }

        const installedPluginPath = await this.getPluginInstallPath();

        if (token && token.isCancellationRequested) {
            return DebugSupportInstallState.cancelled;
        }

        if (!(await exists(installedPluginPath))) {
            return DebugSupportInstallState.missing;
        }

        if (token && token.isCancellationRequested) {
            return DebugSupportInstallState.cancelled;
        }

        if (progress) {
            progress.report({ increment: 10, message: 'Validating existing install...' });
        }

        const installedHash = await md5File(installedPluginPath);
        const bundledHash = await md5File(this.getBundledPluginPath());

        if (token && token.isCancellationRequested) {
            return DebugSupportInstallState.cancelled;
        }

        if (installedHash !== bundledHash) {
            return DebugSupportInstallState.incorrectVersion;
        }

        return DebugSupportInstallState.installed;
    }

    async installPlugin(
        progress?: Progress<{ increment: number; message: string }>,
        token?: CancellationToken
    ): Promise<void> {
        const pluginInstallPath = await this.getPluginInstallPath();
        const bundledPluginPath = await this.getBundledPluginPath();

        if (token && token.isCancellationRequested) {
            return;
        }

        if (progress) {
            progress.report({ increment: 50, message: 'Copying to F4SE plugin path...' });
        }

        await copyFile(bundledPluginPath, pluginInstallPath);
    }
}

export const IDebugSupportInstaller = createDecorator<IDebugSupportInstaller>('debugSupportInstaller');
