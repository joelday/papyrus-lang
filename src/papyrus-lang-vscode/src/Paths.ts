import * as fs from 'fs';
import * as path from 'path';

import { workspace, ExtensionContext, Uri } from 'vscode';

import { PapyrusGame } from './PapyrusGame';
import { inDevelopmentEnvironment } from './Utilities';

import winreg from 'winreg';
import { promisify } from 'util';
import { CancellationToken } from 'vscode-jsonrpc';


const exists = promisify(fs.exists);


/************************************************************************* */
/*** Internal paths                                                        */
/************************************************************************* */

function getToolGameName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim';
    }
}

export function getLanguageToolPath(game: PapyrusGame): string {
    const toolGameName = getToolGameName(game);
    return `./bin/Debug/net461/DarkId.Papyrus.Host.${toolGameName}/DarkId.Papyrus.Host.${toolGameName}.exe`;
}

export function getDebugToolPath(game: PapyrusGame): string {
    const toolGameName = getToolGameName(game);
    return `./debug-bin/Debug/net461/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}.exe`;
}

export function getPyroCliPath(): string {
    return './pyro/pyro_cli/pyro_cli.exe';
}

export function getPyroDirPath(): string {
    return './pyro';
}

export function getResourceDir(): string {
    return './resources';
}

export function getWelcomeFile(): string {
    return path.join(getResourceDir(), 'welcome', 'index.md');
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
