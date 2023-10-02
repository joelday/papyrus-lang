import path from 'path';
import {
    getRegistryKeyForGame,
    PapyrusGame,
    GameVariant,
    GetUserGameFolderName,
    getScriptExtenderName,
} from '../PapyrusGame';
import * as fs from 'fs';
import { promisify } from 'util';
import {
    AddressLibAssetSuffix,
    AddressLibraryF4SEModName,
    AddressLibraryName,
    AddressLibrarySKSEAEModName,
    AddressLibrarySKSEModName,
} from './constants';
import { INIData } from './INIHelpers';
import { getHomeFolder, getRegistryValueData } from './OSHelpers';

const exists = promisify(fs.exists);
const readdir = promisify(fs.readdir);
const readFile = promisify(fs.readFile);

export function getAsssetLibraryDLSuffix(addlibname: AddressLibraryName): AddressLibAssetSuffix {
    switch (addlibname) {
        case AddressLibrarySKSEModName:
            return AddressLibAssetSuffix.SkyrimSE;
        case AddressLibrarySKSEAEModName:
            return AddressLibAssetSuffix.SkyrimAE;
        case AddressLibraryF4SEModName:
            return AddressLibAssetSuffix.Fallout4;
    }
}

export function getAddressLibNameFromAssetSuffix(suffix: AddressLibAssetSuffix): AddressLibraryName {
    switch (suffix) {
        case AddressLibAssetSuffix.SkyrimSE:
            return AddressLibrarySKSEModName;
        case AddressLibAssetSuffix.SkyrimAE:
            return AddressLibrarySKSEAEModName;
        case AddressLibAssetSuffix.Fallout4:
            return AddressLibraryF4SEModName;
    }
}

export function getAddressLibNames(game: PapyrusGame): AddressLibraryName[] {
    if (game === PapyrusGame.fallout4) {
        return [AddressLibraryF4SEModName];
    } else if (game === PapyrusGame.skyrimSpecialEdition) {
        return [AddressLibrarySKSEModName, AddressLibrarySKSEAEModName];
    }
    // there is no skyrim classic address library
    return [];
}

export function CheckIfDebuggingIsEnabledInIni(iniData: INIData) {
    return (
        iniData.Papyrus.bLoadDebugInformation === 1 &&
        iniData.Papyrus.bEnableTrace === 1 &&
        iniData.Papyrus.bEnableLogging === 1
    );
}

export function TurnOnDebuggingInIni(skyrimIni: INIData) {
    const _ini = structuredClone(skyrimIni);
    _ini.Papyrus.bLoadDebugInformation = 1;
    _ini.Papyrus.bEnableTrace = 1;
    _ini.Papyrus.bEnableLogging = 1;
    return _ini;
}

export async function FindUserGamePath(game: PapyrusGame, variant: GameVariant): Promise<string | null> {
    const GameFolderName: string = GetUserGameFolderName(game, variant);
    const home = getHomeFolder();
    if (!home) {
        return null;
    }
    const userGamePath = path.join(home, 'Documents', 'My Games', GameFolderName);
    if (await exists(userGamePath)) {
        return userGamePath;
    }
    return null;
}

/**
 * We need to determine variants for things like the save game path
 * @param game
 * @param installPath
 * @returns
 */
export async function DetermineGameVariant(game: PapyrusGame, installPath: string): Promise<GameVariant> {
    // only Skyrim SE has variants, the rest are only sold on steam
    if (game !== PapyrusGame.skyrimSpecialEdition) {
        return GameVariant.Steam;
    }
    if (!installPath || !(await exists(installPath))) {
        // just default to steam
        return GameVariant.Steam;
    }
    const gog_dll = path.join(installPath, 'Galaxy64.dll');
    const epic_dll = path.join(installPath, 'EOSSDK-Win64-Shipping.dll');
    if (await exists(gog_dll)) {
        return GameVariant.GOG;
    }
    if (await exists(epic_dll)) {
        return GameVariant.Epic;
    }
    // default to steam
    return GameVariant.Steam;
}

async function findSkyrimSEEpic(): Promise<string | null> {
    const key = `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}Epic Games\\EpicGamesLauncher`;
    const val = 'AppDataPath';
    const epicAppdatapath = await getRegistryValueData(key, val);
    let manifestsDir: string;
    if (epicAppdatapath) {
        manifestsDir = path.join(epicAppdatapath, 'Manifests');
    } else if (process.env.PROGRAMDATA) {
        // if the local app data path isn't set, try the global one
        manifestsDir = path.join(process.env.PROGRAMDATA, 'Epic', 'EpicGamesLauncher', 'Data', 'Manifests');
    } else {
        return null;
    }
    if (await exists(manifestsDir)) {
        // list the directory and find the manifest for Skyrim SE
        const manifestFiles = await readdir(manifestsDir);
        for (const manifestFile of manifestFiles) {
            // read the manifest file and check if it's for Skyrim SE
            if (path.extname(manifestFile) !== '.item') {
                continue;
            }
            const data = await readFile(path.join(manifestsDir, manifestFile), 'utf8');
            if (data) {
                const manifest = JSON.parse(data);
                if (
                    manifest &&
                    manifest.AppName &&
                    (manifest.AppName === 'ac82db5035584c7f8a2c548d98c86b2c' ||
                        manifest.AppName === '5d600e4f59974aeba0259c7734134e27')
                ) {
                    if (manifest.InstallLocation && (await exists(manifest.InstallLocation))) {
                        return manifest.InstallLocation;
                    }
                }
            }
        }
    }
    return null;
}

async function findSkyrimSEGOG(): Promise<string | null> {
    const keynames = [
        // check Skyrim AE first
        `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}GOG.com\\Games\\1162721350`,
        // If AE isn't installed, check Skyrim SE
        `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}GOG.com\\Games\\1711230643`,
    ];
    for (const key of keynames) {
        const gogpath = await getRegistryValueData(key, 'path');
        if (gogpath && (await exists(gogpath))) {
            return gogpath;
        }
    }
    return null;
}

async function FindGameSteamPath(game: PapyrusGame): Promise<string | null> {
    const key = `\\SOFTWARE\\${
        process.arch === 'x64' ? 'WOW6432Node\\' : ''
    }Bethesda Softworks\\${getRegistryKeyForGame(game)}`;
    const val = 'installed path';
    const pathValue = await getRegistryValueData(key, val);
    if (pathValue && (await exists(pathValue))) {
        return pathValue;
    }
    return null;
}

export async function FindGamePath(game: PapyrusGame) {
    if (game === PapyrusGame.fallout4 || game === PapyrusGame.skyrim) {
        return FindGameSteamPath(game);
    } else if (game === PapyrusGame.skyrimSpecialEdition) {
        let path = await FindGameSteamPath(game);
        if (path) {
            return path;
        }
        path = await findSkyrimSEGOG();
        if (path) {
            return path;
        }
        path = await findSkyrimSEEpic();
        if (path) {
            return path;
        }
    }
    return null;
}

/**
 * Will return the path to the plugins folder for the given game relative to the game's data folder
 *
 *
 * @param game fallout4 or skyrimse
 * @returns
 */
export function getRelativePluginPath(game: PapyrusGame) {
    return `${getScriptExtenderName(game)}/Plugins`;
}
