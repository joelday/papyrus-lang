export enum PapyrusGame {
    fallout4 = 'fallout4',
    skyrim = 'skyrim',
    skyrimSpecialEdition = 'skyrimSpecialEdition',
}
const displayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim Special Edition/Anniversary Edition'],
]);

export function getDisplayNameForGame(game: PapyrusGame) {
    return displayNames.get(game);
}

const shortDisplayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim SE/AE'],
]);

const scriptExtenderNames = new Map([
    [PapyrusGame.fallout4, 'F4SE'],
    [PapyrusGame.skyrimSpecialEdition, 'SKSE'],
]);

export function getScriptExtenderName(game: PapyrusGame) {
    return scriptExtenderNames.get(game);
}
const scriptExtenderExecutableNames = new Map([
    [PapyrusGame.fallout4, 'f4se_loader.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'skse64_loader.exe'],
]);

export function getScriptExtenderExecutableName(game: PapyrusGame) {
    return scriptExtenderExecutableNames.get(game);
}
const scriptExtenderUrls = new Map([
    [PapyrusGame.fallout4, 'https://f4se.silverlock.org/'],
    [PapyrusGame.skyrimSpecialEdition, 'https://skse.silverlock.org/'],
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

export function getRegistryKeyForGame(game: PapyrusGame) {
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

export function getDefaultFlagsFileNameForGame(game: PapyrusGame) {
    return game === PapyrusGame.fallout4 ? 'Institute_Papyrus_Flags.flg' : 'TESV_Papyrus_Flags.flg';
}

const executableNames = new Map([
    [PapyrusGame.skyrim, 'Skyrim.exe'],
    [PapyrusGame.fallout4, 'Fallout4.exe'],
    [PapyrusGame.skyrimSpecialEdition, 'SkyrimSE.exe'],
]);

export function getExecutableNameForGame(game: PapyrusGame) {
    return executableNames.get(game)!;
}

export function getGameIniName(game: PapyrusGame): string {
    return game == PapyrusGame.fallout4 ? 'fallout4.ini' :'skyrim.ini';
}

// TODO: Support VR
export enum GameVariant {
    Steam = "Steam",
    GOG = "GOG",
    Epic = "Epic Games"
}

/**
 * returns the name of the Game Save folder for the given variant
 * @param variant 
 * @returns 
 */
export function GetUserGameFolderName(game: PapyrusGame, variant: GameVariant){
    switch (game) {
        case PapyrusGame.fallout4:
            return "Fallout4";
        case PapyrusGame.skyrim:
            return "Skyrim";
        case PapyrusGame.skyrimSpecialEdition:
            switch (variant) {
                case GameVariant.Steam:
                return "Skyrim Special Edition";
                case GameVariant.GOG:
                return "Skyrim Special Edition GOG";
                case GameVariant.Epic:
                return "Skyrim Special Edition EPIC";
            }
    }
}
