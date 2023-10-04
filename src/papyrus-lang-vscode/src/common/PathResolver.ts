import * as fs from 'fs';
import * as path from 'path';

import { inject, injectable, interfaces } from 'inversify';
import { take } from 'rxjs/operators';
import { promisify } from 'util';

import { ExtensionContext } from 'vscode';
import { IExtensionContext } from '../common/vscode/IocDecorators';

import { PapyrusGame, getScriptExtenderName } from '../PapyrusGame';
import { inDevelopmentEnvironment } from '../Utilities';
import { IExtensionConfigProvider, IGameConfig } from '../ExtensionConfigProvider';
import { PDSModName } from './constants';
import { DetermineGameVariant, FindGamePath, FindUserGamePath } from './GameHelpers';

const exists = promisify(fs.exists);

export interface IPathResolver {
    // Internal paths
    getDebugPluginBundledPath(game: PapyrusGame): Promise<string>;
    getAddressLibraryDownloadFolder(): Promise<string>;
    getAddressLibraryDownloadJSON(): Promise<string>;
    getLanguageToolPath(game: PapyrusGame): Promise<string>;
    getDebugToolPath(game: PapyrusGame): Promise<string>;
    getPyroCliPath(): Promise<string>;
    getPyroDirPath(): Promise<string>;
    getResourceDir(): Promise<string>;
    getWelcomeFile(): Promise<string>;
    // External paths
    getInstallPath(game: PapyrusGame): Promise<string | null>;
    getUserGamePath(game: PapyrusGame): Promise<string | null>;
    getModDirectoryPath(game: PapyrusGame): Promise<string | null>;
    getModParentPath(game: PapyrusGame): Promise<string | null>;
    getDebugPluginInstallPath(game: PapyrusGame, legacy?: boolean): Promise<string | null>;
}

@injectable()
export class PathResolver implements IPathResolver {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;

    constructor(
        @inject(IExtensionConfigProvider) configProvider: IExtensionConfigProvider,
        @inject(IExtensionContext) context: ExtensionContext
    ) {
        this._configProvider = configProvider;
        this._context = context;
    }

    private async _getGameConfig(game: PapyrusGame): Promise<IGameConfig> {
        return (await this._configProvider.config.pipe(take(1)).toPromise())[game];
    }

    private async _getExtensionPath(): Promise<string> {
        return this._context.extensionPath;
    }

    private async _asExtensionAbsolutePath(path: string): Promise<string> {
        return this._context.asAbsolutePath(path);
    }

    private async _getExtenderPluginPath(game: PapyrusGame) {
        return `Data/${getScriptExtenderName(game)}/Plugins`;
    }

    // TODO: Refactor this properly
    // For mod managers. The whole directory for the mod is "Data" so omit that part.
    public static _getModMgrExtenderPluginRelativePath(game: PapyrusGame) {
        return `${getScriptExtenderName(game)}/Plugins`;
    }
    // Public Methods

    /************************************************************************* */
    /*** Internal paths                                                        */
    /************************************************************************* */

    public async getDebugPluginBundledPath(game: PapyrusGame) {
        const dll = getPluginDllName(game);
        if (!dll) {
            throw new Error('Debugging not supported for game ' + game);
        }
        return this._asExtensionAbsolutePath(path.join(bundledPluginPath, dll));
    }

    public async getAddressLibraryDownloadFolder() {
        return this._asExtensionAbsolutePath(downloadedAddressLibraryPath);
    }

    public async getAddressLibraryDownloadJSON() {
        return this._asExtensionAbsolutePath(path.join(downloadedAddressLibraryPath, addlibManifestName));
    }

    public async getLanguageToolPath(game: PapyrusGame): Promise<string> {
        const toolGameName = getToolGameName(game);
        return this._asExtensionAbsolutePath(
            `./bin/Debug/net472/DarkId.Papyrus.Host.${toolGameName}/DarkId.Papyrus.Host.${toolGameName}.exe`
        );
    }

    public async getDebugToolPath(game: PapyrusGame): Promise<string> {
        const toolGameName = getToolGameName(game);
        return this._asExtensionAbsolutePath(
            `./debug-bin/Debug/net472/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}/` +
                `DarkId.Papyrus.DebugAdapterProxy.${toolGameName}.exe`
        );
    }

    public async getPyroCliPath(): Promise<string> {
        return this._asExtensionAbsolutePath('./pyro/pyro.exe');
    }

    public async getPyroDirPath(): Promise<string> {
        return this._asExtensionAbsolutePath('./pyro');
    }

    public async getResourceDir(): Promise<string> {
        return this._asExtensionAbsolutePath('./resources');
    }

    public async getWelcomeFile(): Promise<string> {
        return path.join(await this.getResourceDir(), 'welcome', 'index.md');
    }

    /************************************************************************* */
    /*** External paths (ones that are not "ours")                             */
    /************************************************************************* */

    public async getInstallPath(game: PapyrusGame): Promise<string | null> {
        const config = await this._getGameConfig(game);

        return resolveInstallPath(game, config.installPath, this._context);
    }

    public async getUserGamePath(game: PapyrusGame): Promise<string | null> {
        const config = await this._getGameConfig(game);
        return resolveUserGamePath(game, config.installPath, this._context);
    }

    // TODO: Refactor this properly.
    public async getDebugPluginInstallPath(game: PapyrusGame, legacy?: boolean): Promise<string | null> {
        const modDirectoryPath = await this.getModDirectoryPath(game);

        if (modDirectoryPath) {
            return path.join(
                modDirectoryPath,
                PDSModName,
                PathResolver._getModMgrExtenderPluginRelativePath(game),
                getPluginDllName(game, legacy)
            );
        } else {
            const installPath = await this.getInstallPath(game);
            if (!installPath) {
                return null;
            }

            return path.join(installPath, await this._getExtenderPluginPath(game), getPluginDllName(game, legacy));
        }
    }

    public async getModDirectoryPath(game: PapyrusGame) {
        const config = await this._getGameConfig(game);
        if (!config) {
            return null;
        }

        return config.modDirectoryPath;
    }

    /**
     * If the mod directory is set, then this just returns the mod directory
     * Otherwise, it returns "${game directory}/Data"
     * @param game
     * @returns
     */
    public async getModParentPath(game: PapyrusGame): Promise<string | null> {
        const modDirectoryPath = await this.getModDirectoryPath(game);
        if (modDirectoryPath) {
            return modDirectoryPath;
        }
        const installPath = await this.getInstallPath(game);
        if (!installPath) {
            return null;
        }
        return path.join(installPath, 'Data');
    }
    dispose() {}
}

export const IPathResolver: interfaces.ServiceIdentifier<IPathResolver> = Symbol('pathResolver');

/************************************************************************* */
/*** Internal paths                                                        */
/************************************************************************* */

const bundledPluginPath = 'debug-plugin';
const downloadedAddressLibraryPath = 'debug-address-library';
const addlibManifestName = 'address-library.json';
export function getPluginDllName(game: PapyrusGame, legacy = false) {
    switch (game) {
        case PapyrusGame.fallout4:
            return legacy ? 'DarkId.Papyrus.DebugServer.dll' : 'DarkId.Papyrus.DebugServer.Fallout4.dll';
        case PapyrusGame.skyrimSpecialEdition:
            return 'DarkId.Papyrus.DebugServer.Skyrim.dll';
        default:
            throw new Error('Debugging plugin not implemented for game ' + game);
    }
}

function getToolGameName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim';
        case PapyrusGame.starfield:
            return 'Starfield';
    }
}

/************************************************************************* */
/*** External paths (ones that are not "ours")                             */
/************************************************************************* */

export async function resolveInstallPath(
    game: PapyrusGame,
    installPath: string,
    context: ExtensionContext
): Promise<string | null> {
    if (await exists(installPath)) {
        return installPath;
    }
    const pathValue = await FindGamePath(game);
    if (pathValue) {
        return pathValue;
    }

    // TODO: @joelday, what is this for?
    if (inDevelopmentEnvironment() && game !== PapyrusGame.skyrim) {
        return context.asAbsolutePath('../../dependencies/compilers');
    }

    return null;
}

async function resolveUserGamePath(
    game: PapyrusGame,
    installPath: string,
    context: ExtensionContext
): Promise<string | null> {
    let _installPath: string | null = installPath;
    if (!(await exists(installPath))) {
        _installPath = await resolveInstallPath(game, installPath, context);
    }
    if (!installPath) {
        return null;
    }
    const variant = await DetermineGameVariant(game, installPath);
    return FindUserGamePath(game, variant);
}

export function pathToOsPath(pathName: string) {
    return path.format(path.parse(pathName));
}
