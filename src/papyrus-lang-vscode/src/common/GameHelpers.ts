import path from 'path';
import {
    getRegistryKeyForGame,
    PapyrusGame,
    GameVariant,
    GetUserGameFolderName,
    getScriptExtenderName,
    getInstalledPathRegVal,
    getGameIniName,
    getGameCustomIniName,
    getGamePrefsIniName,
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
import { INIData, ParseIniFile, WriteChangesToIni } from './INIHelpers';
import { getHomeFolder, getRegistryValueData } from './OSHelpers';
import structuredClone from '@ungap/structured-clone';

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

/**
 * Checks if this is, in fact, a valid game user dir
 */
export async function CheckValidGameUserDir(game: PapyrusGame, userPath: string): Promise<boolean> {
    if (!userPath || !(await exists(userPath))) {
        return false;
    }
    const gameIniPath = path.join(userPath, getGameIniName(game));
    const customIni = path.join(userPath, getGameCustomIniName(game));
    const prefsIni = path.join(userPath, getGamePrefsIniName(game));
    const someInisExist = (await exists(gameIniPath)) || (await exists(customIni)) || (await exists(prefsIni));
    // each game creates at LEAST the prefsIni file when starting for the first time; if it doesn't exist, it's not a valid game user dir
    return someInisExist;
}

/**
 * Checks game configs for user debugging settings
 * Checks both the game's base ini and the custom ini
 *
 * @param game - the game to check
 * @param userPath - the path to the user's game folder containing the ini files
 */
export async function CheckGameConfigForDebug(game: PapyrusGame, userPath: string): Promise<boolean> {
    if (!(await CheckValidGameUserDir(game, userPath))) return false;

    // Starfield ignores Starfield.ini in the user's user dir, so we have to check ONLY the custom ini
    if (game !== PapyrusGame.starfield) {
        const gameIniPath = path.join(userPath, getGameIniName(game));

        if (await CheckIniFileForDebug(game, gameIniPath)) {
            return true;
        }
    }
    const customIni = path.join(userPath, getGameCustomIniName(game));
    return await CheckIniFileForDebug(game, customIni);
}

/**
 * Check <game>.ini (or <game>Custom.ini) for user debugging settings
 */
export async function CheckIniFileForDebug(game: PapyrusGame, iniPath: string): Promise<boolean> {
    if (fs.existsSync(iniPath)) {
        const iniData: INIData | undefined = await ParseIniFile(iniPath);
        if (iniData) {
            if (CheckINIDataForDebug(game, iniData)) {
                return true;
            }
        }
    }
    return false;
}

export function CheckINIDataForDebug(game: PapyrusGame, iniData: INIData) {
    let check = iniData && iniData.Papyrus && iniData.Papyrus.bLoadDebugInformation == '1';
    // We don't necessarily need these
    // && iniData.Papyrus.bEnableTrace == '1' && iniData.Papyrus.bEnableLogging == '1';
    if (game == PapyrusGame.starfield) {
        check = check && iniData.Papyrus.bEnableRemoteDebugging == '1';
        check = check && iniData.Papyrus.iRemoteDebuggingPort == '20548'; // TODO: Starfield: make this dynamic; we should just read the set port from the ini
    }
    return check;
}

export async function ConfigureDebug(game: PapyrusGame, gameUserDir: string): Promise<boolean> {
    const customIniPath = path.join(gameUserDir, getGameCustomIniName(game));
    // Double check to make sure that the user hasn't already enabled debugging
    if (await CheckIniFileForDebug(game, customIniPath)) {
        return true;
    }
    let iniData: INIData | undefined;
    if (!(await exists(customIniPath))) {
        // we need to make it
        iniData = {};
        iniData.Papyrus = {};
    } else {
        iniData = await ParseIniFile(customIniPath);
        if (!iniData) {
            // Don't try and recover, just fail
            // If it's a corrupt game ini file, the user needs to fix it
            return false;
        }
    }
    const newinidata = TurnOnDebuggingInIni(game, iniData);
    return await WriteChangesToIni(customIniPath, newinidata);
}

export function TurnOnDebuggingInIni(game: PapyrusGame, iniData: INIData) {
    const _ini = structuredClone(iniData) || {};
    if (!_ini.Papyrus) {
        _ini.Papyrus = {};
    }
    _ini.Papyrus.bLoadDebugInformation = 1;
    _ini.Papyrus.bEnableTrace = 1;
    _ini.Papyrus.bEnableLogging = 1;
    if (game === PapyrusGame.starfield) {
        _ini.Papyrus.bRemoteDebuggingLogVerbose = 1;
        _ini.Papyrus.bEnableRemoteDebugging = 1;
        _ini.Papyrus.iRemoteDebuggingPort = 20548; // TODO: STarfield: make this dynamic
    }
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
    // TODO: Starfield: implement support for gamepass version
    // only Skyrim SE has variants, the rest are only sold on steam
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
    const key = getRegistryKeyForGame(game);
    const val = getInstalledPathRegVal(game);
    const pathValue = await getRegistryValueData(key, val);
    if (pathValue && (await exists(pathValue))) {
        return pathValue;
    }
    return null;
}

export async function FindGamePath(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
        case PapyrusGame.skyrim:
            return FindGameSteamPath(game);
        // TODO: support gamepass version of starfield
        case PapyrusGame.starfield:
            return FindGameSteamPath(game);
        case PapyrusGame.skyrimSpecialEdition:
            {
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
            break;
        default:
            break;
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
