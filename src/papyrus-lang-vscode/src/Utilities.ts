import * as path from 'path';
import * as fs from 'fs';
import { PapyrusGame } from './PapyrusGame';

import winreg from 'winreg';
import { promisify } from 'util';
const exists = promisify(fs.exists);

function getCompilerAssemblyPath(installPath: string, compilerPath: string) {
    return path.join(installPath, compilerPath);
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

async function resolveInstallPathWithRegistryFallback(game: PapyrusGame, installPath: string) {
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

    return null;
}

export interface IInstallInfo {
    installPath: string;
    compilerAssemblyPath: string;
}

export async function getInstallInfo(game: PapyrusGame, installPath: string, compilerPath: string) {
    const resolvedInstallPath = await resolveInstallPathWithRegistryFallback(game, installPath);
    const compilerAssemblyPath = resolvedInstallPath && getCompilerAssemblyPath(resolvedInstallPath, compilerPath);
    const compilerExists = compilerAssemblyPath && (await exists(compilerAssemblyPath));

    return {
        installPath: resolvedInstallPath,
        compilerAssemblyPath: compilerExists ? compilerAssemblyPath : null,
    };
}
