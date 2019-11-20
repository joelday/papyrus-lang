import * as fs from 'fs';
import { promisify } from 'util';

import { xml2js } from 'xml-js';

import { Uri } from 'vscode';

import { PyroGameToPapyrusGame } from './features/PyroTaskDefinition';

const readFile = promisify(fs.readFile);

export enum PapyrusGame {
    fallout4 = 'fallout4',
    skyrim = 'skyrim',
    skyrimSpecialEdition = 'skyrimSpecialEdition',
}

const displayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim Special Edition'],
]);

export function getDisplayNameForGame(game: PapyrusGame) {
    return displayNames.get(game);
}

const shortDisplayNames = new Map([
    [PapyrusGame.fallout4, 'Fallout 4'],
    [PapyrusGame.skyrim, 'Skyrim'],
    [PapyrusGame.skyrimSpecialEdition, 'Skyrim SE'],
]);

const scriptExtenderNames = new Map([[PapyrusGame.fallout4, 'F4SE'], [PapyrusGame.skyrimSpecialEdition, 'SKSE']]);

export function getScriptExtenderName(game: PapyrusGame) {
    return scriptExtenderNames.get(game);
}

const scriptExtenderUrls = new Map([
    [PapyrusGame.fallout4, 'https://f4se.silverlock.org/'],
    [PapyrusGame.skyrimSpecialEdition, 'https://skse.silverlock.org/'],
]);

export function getScriptExtenderUrl(game: PapyrusGame) {
    return scriptExtenderUrls.get(game);
}

export function getShortDisplayNameForGame(game: PapyrusGame) {
    return shortDisplayNames.get(game);
}

export function getGames(): PapyrusGame[] {
    return Object.keys(PapyrusGame).map((k) => PapyrusGame[k]);
}

export async function getWorkspaceGame(ppjFiles: Uri[]): Promise<PapyrusGame | undefined> {
    let game: string = undefined;
    if (ppjFiles.length) {
        // Just use the first one we find because they should be all the same game.
        // (Except for multiroot workspaces that mix games which we don't support yet.)
        let ppjFile: Uri = ppjFiles[0];
        let xml = await readFile(ppjFile.fsPath, { encoding: 'utf-8' });
        let results = xml2js(xml, { compact: true, trim: true });
        game = results['PapyrusProject']['_attributes']['Game'];
    }
    return PyroGameToPapyrusGame[game];
}