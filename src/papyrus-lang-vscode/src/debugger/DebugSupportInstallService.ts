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
const readdir = promisify(fs.readdir);

function getPluginDataDir(pluginPath: string): string {
    return path.join(path.dirname(pluginPath), 'data');
}

async function listJsonFileNames(directoryPath: string): Promise<string[]> {
    if (!(await exists(directoryPath))) {
        return [];
    }

    return (await readdir(directoryPath)).filter((fileName) => fileName.endsWith('.json')).sort();
}

// Extra installed JSON files are allowed (a new game version can be a dropped data file).
// Missing or changed bundled files must prompt a recopy.
async function bundledDataIsInstalled(bundledDataDir: string, installedDataDir: string): Promise<boolean> {
    const bundledJson = await listJsonFileNames(bundledDataDir);
    if (bundledJson.length === 0) {
        return false;
    }

    for (const fileName of bundledJson) {
        const installedPath = path.join(installedDataDir, fileName);
        if (!(await exists(installedPath))) {
            return false;
        }

        if ((await md5File(path.join(bundledDataDir, fileName))) !== (await md5File(installedPath))) {
            return false;
        }
    }

    return true;
}

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
        const bundledHash = await md5File(bundledPluginPath);

        if (installedHash !== bundledHash) {
            return DebugSupportInstallState.incorrectVersion;
        }

        if (!(await bundledDataIsInstalled(getPluginDataDir(bundledPluginPath), getPluginDataDir(installedPluginPath)))) {
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

        const bundledDataDir = getPluginDataDir(bundledPluginPath);
        const bundledJson = await listJsonFileNames(bundledDataDir);
        if (bundledJson.length === 0) {
            throw new Error(
                `Address Library data files are missing from the extension bundle (${bundledDataDir}). ` +
                    'The debug plugin cannot start without versioned ID JSON.'
            );
        }

        await mkdirIfNeeded(path.dirname(pluginInstallPath));
        await copyFile(bundledPluginPath, pluginInstallPath);

        const installDataDir = getPluginDataDir(pluginInstallPath);
        await mkdirIfNeeded(installDataDir);
        for (const fileName of bundledJson) {
            await copyFile(path.join(bundledDataDir, fileName), path.join(installDataDir, fileName));
        }

        return true;
    }
}

export const IDebugSupportInstallService: interfaces.ServiceIdentifier<IDebugSupportInstallService> =
    Symbol('debugSupportInstallService');
