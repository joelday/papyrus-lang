export enum PapyrusGame {
    fallout4 = 'fallout4',
    skyrim = 'skyrim',
    skyrimSpecialEdition = 'skyrimSpecialEdition',
    starfield = 'starfield',
}

const displayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim Special Edition/Anniversary Edition'],
    [PapyrusGame.starfield, 'Starfield'],
]);

export function getDisplayNameForGame(game: PapyrusGame) {
    return displayNames.get(game);
}

const shortDisplayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim SE/AE'],
    [PapyrusGame.starfield, 'Starfield'],
]);

const scriptExtenderNames = new Map([
    [PapyrusGame.fallout4, 'F4SE'],
    [PapyrusGame.skyrimSpecialEdition, 'SKSE'],
    [PapyrusGame.starfield, 'SFSE'],
]);

export function getScriptExtenderName(game: PapyrusGame) {
    return scriptExtenderNames.get(game);
}
const scriptExtenderExecutableNames = new Map([
    [PapyrusGame.fallout4, 'f4se_loader.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'skse64_loader.exe'],
    [PapyrusGame.starfield, 'sfse_loader.exe'],
]);

export function getScriptExtenderExecutableName(game: PapyrusGame) {
    return scriptExtenderExecutableNames.get(game);
}
const scriptExtenderUrls = new Map([
    [PapyrusGame.fallout4, 'https://f4se.silverlock.org/'],
    [PapyrusGame.skyrimSpecialEdition, 'https://skse.silverlock.org/'],
    [PapyrusGame.starfield, 'https://sfse.silverlock.org/'],
]);

export function getScriptExtenderUrl(game: PapyrusGame) {
    return scriptExtenderUrls.get(game)!;
}

export function getShortDisplayNameForGame(game: PapyrusGame): string {
    return shortDisplayNames.get(game)!;
}

export function getGames(): PapyrusGame[] {
    return (Object.keys(PapyrusGame) as (keyof typeof PapyrusGame)[]).map((k) => PapyrusGame[k]);
}

const commonOldRegPrefix = `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}Bethesda Softworks\\`;

export function getRegistryKeyForGame(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return commonOldRegPrefix + 'Fallout4';
        case PapyrusGame.skyrim:
            return commonOldRegPrefix + 'Skyrim';
        case PapyrusGame.skyrimSpecialEdition:
            return commonOldRegPrefix + 'Skyrim Special Edition';
        case PapyrusGame.starfield:
            return '\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 1716740';
    }
}

export function getInstalledPathRegVal(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'installed path';
        case PapyrusGame.starfield:
            return 'InstallLocation';
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
        // TODO: Starfield: figure out what this is
        case PapyrusGame.starfield:
            return 'UNKNOWN_REPLACE_ME';
    }
}

export function getDefaultFlagsFileNameForGame(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Institute_Papyrus_Flags.flg';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'TESV_Papyrus_Flags.flg';
        // TODO: Starfield: The actual name of it is unknown and won't be known until CK comes out in 2024
        case PapyrusGame.starfield:
            return 'Starfield_Papyrus_Flags.flg';
    }
}

const executableNames = new Map([
    [PapyrusGame.skyrim, 'Skyrim.exe'],
    [PapyrusGame.fallout4, 'Fallout4.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'SkyrimSE.exe'],
    [PapyrusGame.starfield, 'Starfield.exe'],
]);

export function getExecutableNameForGame(game: PapyrusGame) {
    return executableNames.get(game)!;
}

export function getGameIniName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'fallout4.ini';
        case PapyrusGame.skyrim:
            return 'skyrim.ini';
        case PapyrusGame.skyrimSpecialEdition:
            return 'skyrim.ini';
        case PapyrusGame.starfield:
            return 'Starfield.ini';
    }
}

export function getGameCustomIniName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4Custom.ini';
        case PapyrusGame.skyrim:
            return 'SkyrimCustom.ini';
        case PapyrusGame.skyrimSpecialEdition:
            return 'SkyrimCustom.ini';
        case PapyrusGame.starfield:
            return 'StarfieldCustom.ini';
    }
}

export function getGamePrefsIniName(game: PapyrusGame): string {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4Prefs.ini';
        case PapyrusGame.skyrim:
            return 'SkyrimPrefs.ini';
        case PapyrusGame.skyrimSpecialEdition:
            return 'SkyrimPrefs.ini';
        case PapyrusGame.starfield:
            return 'StarfieldPrefs.ini';
    }
}

// TODO: Support VR
export enum GameVariant {
    Steam = 'Steam',
    GOG = 'GOG',
    Epic = 'Epic Games',
    GamePass = 'Game Pass',
}

/**
 * returns the name of the Game Save folder for the given variant
 * Resides in "%USERPROFILE%\Documents\My Games"
 *
 * @param game
 * @param variant
 * @returns string - the name of the game save folder
 */
export function GetUserGameFolderName(game: PapyrusGame, variant: GameVariant) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
            return 'Skyrim';
        case PapyrusGame.skyrimSpecialEdition:
            switch (variant) {
                case GameVariant.Steam:
                    return 'Skyrim Special Edition';
                case GameVariant.GOG:
                    return 'Skyrim Special Edition GOG';
                case GameVariant.Epic:
                    return 'Skyrim Special Edition EPIC';
            }
            break;
        case PapyrusGame.starfield:
            switch (variant) {
                case GameVariant.Steam:
                    return 'Starfield';
                case GameVariant.GamePass: // GamePass uses the same folder, fortunately.
                    return 'Starfield';
            }
    }
    return '';
}
