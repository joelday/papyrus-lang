export enum PapyrusGame {
    fallout4,
    skyrim,
    skyrimSpecialEdition = 4,
}

const displayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim Special Edition'],
]);

export function getDisplayNameForGame(game: PapyrusGame) {
    return displayNames.get(game);
}
