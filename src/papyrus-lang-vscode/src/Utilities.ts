import * as fs from 'fs';
import { PapyrusGame } from './PapyrusGame';

import winreg from 'winreg';
import { promisify } from 'util';
import procList from 'ps-list';

import { ExtensionContext, CancellationTokenSource } from 'vscode';
const exists = promisify(fs.exists);

export function* flatten<T>(arrs: T[][]): IterableIterator<T> {
    for (const arr of arrs) {
        for (const el of arr) {
            yield el;
        }
    }
}

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
    } catch (_) {}

    if (inDevelopmentEnvironment() && game !== PapyrusGame.skyrim) {
        return context.asAbsolutePath('../../dependencies/compilers');
    }

    return null;
}

export function delayAsync(durationMs: number): Promise<void> {
    return new Promise((r) => setTimeout(r, durationMs));
}

export function getDefaultFlagsFileNameForGame(game: PapyrusGame) {
    return game === PapyrusGame.fallout4 ? 'Institute_Papyrus_Flags.flg' : 'TESV_Papyrus_Flags.flg';
}

const executableNames = new Map([
    [PapyrusGame.fallout4, 'Fallout4.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'SkyrimSE.exe'],
]);

export function getExecutableNameForGame(game: PapyrusGame) {
    return executableNames.get(game);
}

export async function getGameIsRunning(game: PapyrusGame) {
    const processList = await procList();
    return processList.some((p) => p.name.toLowerCase() === getExecutableNameForGame(game).toLowerCase());
}

export async function waitWhile(
    func: () => Promise<boolean>,
    cancellationToken = new CancellationTokenSource().token,
    pollingFrequencyMs = 1000
) {
    while ((await func()) && !cancellationToken.isCancellationRequested) {
        await delayAsync(pollingFrequencyMs);
    }
}

export function inDevelopmentEnvironment() {
    return process.execArgv.some((arg) => arg.startsWith('--inspect-brk'));
}

export function toCommandLineArgs(obj: Object): string[] {
    return [].concat(
        ...Object.keys(obj).map((key) => {
            const value = obj[key];

            if (typeof value === 'undefined' || value === null) {
                return [];
            }

            return [
                `--${key}`,
                ...(Array.isArray(value) ? value.map((element) => element.toString()) : [value.toString()]),
            ];
        })
    );
}

function getToolGameName(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim';
    }
}

export function getLanguageToolPath(game: PapyrusGame) {
    const toolGameName = getToolGameName(game);
    return `./bin/Debug/net461/DarkId.Papyrus.Host.${toolGameName}/DarkId.Papyrus.Host.${toolGameName}.exe`;
}

export function getDebugToolPath(game: PapyrusGame) {
    const toolGameName = getToolGameName(game);
    return `./debug-bin/Debug/net461/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}/DarkId.Papyrus.DebugAdapterProxy.${toolGameName}.exe`;
}
