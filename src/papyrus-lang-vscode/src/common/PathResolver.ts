import * as fs from 'fs';
import * as path from 'path';

import { createDecorator } from 'decoration-ioc';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import winreg from 'winreg';
import { promisify } from 'util';

import { workspace, Disposable, ExtensionContext, Uri } from 'vscode';
import { CancellationToken } from 'vscode-jsonrpc';
import { IExtensionContext } from '../common/vscode/IocDecorators';

import { PapyrusGame, getScriptExtenderName } from '../PapyrusGame';
import { inDevelopmentEnvironment } from '../Utilities';
import { ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { IExtensionConfigProvider, IGameConfig, IExtensionConfig } from '../ExtensionConfigProvider';

const exists = promisify(fs.exists);


export interface IPathResolver {
    // Internal paths
    getDebugPluginBundledPath(game: PapyrusGame): Promise<string>;
    getLanguageToolPath(game: PapyrusGame): Promise<string>;
    getDebugToolPath(game: PapyrusGame): Promise<string>;
    getPyroCliPath(): Promise<string>;
    getPyroDirPath(): Promise<string>;
    getResourceDir(): Promise<string>;
    getWelcomeFile(): Promise<string>;
    // External paths
    getInstallPath(game: PapyrusGame): Promise<string>;
    getModDirectoryPath(game: PapyrusGame): Promise<string>;
    getDebugPluginInstallPath(game: PapyrusGame, legacy?: boolean): Promise<string>;
}


export class PathResolver implements IPathResolver {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._configProvider = configProvider;
        this._context = context;
    }

    private async _getExtensionConfig(): Promise<IExtensionConfig> {
        return this._configProvider.config.pipe(take(1)).toPromise();
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

    // For mod managers. The whole directory for the mod is "Data" so omit that part.
    private async _getModMgrExtenderPluginPath(game: PapyrusGame) {
        return `${getScriptExtenderName(game)}/Plugins`;
    }
    // Public Methods

    /************************************************************************* */
    /*** Internal paths                                                        */
    /************************************************************************* */

    public async getDebugPluginBundledPath(game: PapyrusGame) {
        return this._asExtensionAbsolutePath(path.join(bundledPluginPath, getPluginDllName(game)));
    }

    public async getLanguageToolPath(game: PapyrusGame): Promise<string> {
        const toolGameName = getToolGameName(game);
        return this._asExtensionAbsolutePath(
            `./bin/Debug/net461/DarkId.Papyrus.Host.${toolGameName}/DarkId.Papyrus.Host.${toolGameName}.exe`
        );
    }

    public async getDebugToolPath(game: PapyrusGame): Promise<string> {
        const toolGameName = getToolGameName(game);
        return this._asExtensionAbsolutePath(
            `./debug-bin/Debug/net461/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}/`
            + `DarkId.Papyrus.DebugAdapterProxy.${toolGameName}.exe`
        );
    }

    public async getPyroCliPath(): Promise<string> {
        return this._asExtensionAbsolutePath('./pyro/pyro_cli/pyro_cli.exe');
    }

    public async getPyroDirPath(): Promise<string> {
        return this._asExtensionAbsolutePath('./pyro');
    }

    public async getResourceDir(): Promise<string> {
        return this._asExtensionAbsolutePath('./resources');
    }

    public async getWelcomeFile(): Promise<string> {
        return this._asExtensionAbsolutePath(path.join(await this.getResourceDir(), 'welcome', 'index.md'));
    }

    /************************************************************************* */
    /*** External paths (ones that are not "ours")                             */
    /************************************************************************* */

    public async getInstallPath(game: PapyrusGame): Promise<string> {
        return resolveInstallPath(game, (await this._getGameConfig(game)).installPath, this._context);
    }

    public async getDebugPluginInstallPath(game: PapyrusGame, legacy?: boolean) {
        const config = await this._getGameConfig(game);
        if (config.modDirectoryPath) {
            return path.join(
                await this.getModDirectoryPath(game), "Papyrus Debug Extension",
                await this._getModMgrExtenderPluginPath(game),
                getPluginDllName(game, legacy)
            );
        } else {
            path.join(
                await this.getInstallPath(game),
                await this._getExtenderPluginPath(game),
                getPluginDllName(game, legacy)
            );
        }
    }

    public async getModDirectoryPath(game: PapyrusGame) {
        return (await this._getGameConfig(game)).modDirectoryPath;
    }

    dispose() { }
}

export const IPathResolver = createDecorator<IPathResolver>('pathResolver');


/************************************************************************* */
/*** Internal paths                                                        */
/************************************************************************* */

const bundledPluginPath = 'debug-plugin';

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

function getToolGameName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim';
    }
}


/************************************************************************* */
/*** External paths (ones that are not "ours")                             */
/************************************************************************* */

function getRegistryKeyForGame(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
            return 'Skyrim';
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim Special Edition';
    }
}

export function getDevelopmentCompilerFolderForGame(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'fallout4';
        case PapyrusGame.skyrim:
            return 'does-not-exist';
        case PapyrusGame.skyrimSpecialEdition:
            return 'skyrim';
    }
}

export async function resolveInstallPath(
    game: PapyrusGame,
    installPath: string,
    context: ExtensionContext
): Promise<string> {
    if (await exists(installPath)) {
        return installPath;
    }

    const reg = new winreg({
        key: `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}Bethesda Softworks\\${getRegistryKeyForGame(
            game
        )}`,
    });

    try {
        const item = await promisify(reg.get).call(reg, 'installed path');

        if (await exists(item.value)) {
            return item.value;
        }
    } catch (_) { }

    if (inDevelopmentEnvironment() && game !== PapyrusGame.skyrim) {
        return context.asAbsolutePath('../../dependencies/compilers');
    }

    return null;
}

export function getDefaultFlagsFileNameForGame(game: PapyrusGame) {
    return game === PapyrusGame.fallout4 ? 'Institute_Papyrus_Flags.flg' : 'TESV_Papyrus_Flags.flg';
}

const executableNames = new Map([
    [PapyrusGame.skyrim, 'Skyrim.exe'],
    [PapyrusGame.fallout4, 'Fallout4.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'SkyrimSE.exe'],
]);

export function getExecutableNameForGame(game: PapyrusGame) {
    return executableNames.get(game);
}
