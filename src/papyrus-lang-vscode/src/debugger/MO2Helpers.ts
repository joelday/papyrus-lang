import { existsSync } from 'fs';
import path from 'path';
import { PapyrusGame } from '../PapyrusGame';
import { PDSModName } from '../common/constants';
import {
    CheckValidGameUserDir,
    DetermineGameVariant,
    FindUserGamePath,
    getAddressLibNames,
} from '../common/GameHelpers';
import { getEnvFromProcess, getGamePIDs, getPIDforProcessName, getPIDsforFullPath } from '../Utilities';
import * as MO2Lib from '../common/MO2Lib';
import { INIData, ParseIniFile } from '../common/INIHelpers';

// TODO: Verify what the starfield calues are

// ours
export const PapyrusMO2Ids: MO2Lib.MO2LongGameID[] = ['Fallout 4', 'Skyrim Special Edition', 'Skyrim', 'Starfield'];

export function GetMO2GameID(game: PapyrusGame): MO2Lib.MO2LongGameID {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout 4';
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim Special Edition';
        case PapyrusGame.skyrim:
            return 'Skyrim';
        case PapyrusGame.starfield:
            return 'Starfield';
    }
}

export function GetPapyrusGameFromMO2GameID(game: MO2Lib.MO2LongGameID): PapyrusGame | undefined {
    switch (game) {
        case 'Fallout 4':
            return PapyrusGame.fallout4;
        case 'Skyrim Special Edition':
            return PapyrusGame.skyrimSpecialEdition;
        case 'Skyrim':
            return PapyrusGame.skyrim;
        case 'Starfield':
            return PapyrusGame.starfield;
    }
    return undefined;
}

export function checkPDSModExistsAndEnabled(modlist: Array<MO2Lib.ModListItem>) {
    return MO2Lib.checkIfModExistsAndEnabled(modlist, PDSModName);
}

export function checkAddressLibrariesExistAndEnabled(modlist: Array<MO2Lib.ModListItem>, game: PapyrusGame) {
    const names = getAddressLibNames(game);
    for (const name of names) {
        if (!MO2Lib.checkIfModExistsAndEnabled(modlist, name)) {
            return false;
        }
    }
    return true;
}

export function AddRequiredModsToModList(p_modlist: Array<MO2Lib.ModListItem>, game: PapyrusGame) {
    // add the debug adapter to the modlist
    let modlist = p_modlist;
    const addlibsNeeded = !checkAddressLibrariesExistAndEnabled(modlist, game);
    const pdsNeeded = !checkPDSModExistsAndEnabled(modlist);
    if (addlibsNeeded || pdsNeeded) {
        if (pdsNeeded) {
            modlist = MO2Lib.AddOrEnableModInModList(modlist, PDSModName);
        }
        if (addlibsNeeded) {
            const addressLibraryMods = getAddressLibNames(game).map(
                (d) => new MO2Lib.ModListItem(d, MO2Lib.ModEnabledState.enabled)
            );
            modlist = addressLibraryMods.reduce(
                (_modlist, mod) => MO2Lib.AddOrEnableModInModList(_modlist, mod.name),
                modlist
            );
        }
    }
    return modlist;
}

export function GetMO2GameShortIdentifier(game: PapyrusGame): string {
    const gamestring =
        game === PapyrusGame.skyrimSpecialEdition ? 'skyrimse' : PapyrusGame[game].toLowerCase().replace(/ /g, '');
    return gamestring;
}

/**
 * Checks if the game was launched with MO2
 *
 * ModOrganizer launches the game with a modified PATH variable, which is used to load the dlls from the MO2 folder
 * We check for the existence of this PATH component and if it points to the MO2 folder
 * @param game
 * @returns boolean
 */
export async function WasGameLaunchedWithMO2(game: PapyrusGame) {
    // get GamePID
    const pids = await getGamePIDs(game);
    if (!pids || pids.length === 0) {
        return false;
    }
    const pid = pids[0];
    // get env from process
    const otherEnv = await getEnvFromProcess(pid);
    if (!otherEnv) {
        return false;
    }
    const pathVar: string = otherEnv['Path'];
    if (!pathVar) {
        return false;
    }
    const pathVarSplit = pathVar.split(';');
    if (pathVarSplit.length === 0 || !pathVarSplit[0]) {
        return false;
    }
    const firstPath = path.normalize(pathVarSplit[0]);
    if (!firstPath) {
        return false;
    }
    const basename = path.basename(firstPath);
    if (basename.toLowerCase() === 'dlls') {
        const parentdir = path.dirname(firstPath);
        const MO2EXEPath = path.join(parentdir, MO2Lib.MO2EXEName);
        if (existsSync(MO2EXEPath)) {
            return true;
        }
    }
    return false;
}

export async function GetPossibleMO2InstancesForModFolder(
    modsFolder: string,
    game: PapyrusGame
): Promise<MO2Lib.MO2InstanceInfo[] | undefined> {
    const gameId = GetMO2GameID(game);
    const instances = (await MO2Lib.FindAllKnownMO2EXEandInstanceLocations(gameId)).reduce((acc, val) => {
        // Combine all the instances together, check to see if the mods folder is in the instance
        return acc.concat(val.instances.filter((d) => d.modsFolder === modsFolder));
    }, [] as Array<MO2Lib.MO2InstanceInfo>);
    if (instances.length === 0) {
        return undefined;
    }
    //filter out the dupes from instances by comparing iniPaths
    const filteredInstances = instances.filter((d, i) => {
        return instances.findIndex((e) => e.iniPath === d.iniPath) === i;
    });
    return filteredInstances;
}

export async function getUserGameDirFromMO2Profile(
    game: PapyrusGame,
    gamePath: string,
    profileFolder: string
): Promise<string> {
    // Game ini paths for MO2 are different depending on whether the profile has local settings or not
    // if [General] LocalSettings=false, then the game ini is in the global game save folder
    // if [General] LocalSettings=true, then the game ini is in the profile folder

    const settingsFile = path.join(profileFolder, 'settings.ini');
    const settingsIniData = await getMO2ProfileSettingsData(settingsFile);
    if (!settingsIniData) {
        throw new Error(`Could not get settings ini data`);
    }

    if (settingsIniData.General.LocalSettings === false) {
        // We don't have local game ini settings, so we need to use the global ones
        const variant = await DetermineGameVariant(game, gamePath);
        const gameSaveDir = (await FindUserGamePath(game, variant)) || '';
        if (!CheckValidGameUserDir(game, gameSaveDir)) {
            throw new Error(
                `MO2 profile does not have local game INI settings, but could not find the global game save directory at ${gameSaveDir} (Try running the game once to generate the ini file)`
            );
        }
        profileFolder = gameSaveDir;
    } else {
        // TODO: This is fixable by running `ModOrganizer.exe refresh`
        if (!CheckValidGameUserDir(game, profileFolder)) {
            throw new Error(`MO2 profile has local game INI settings, but could not find the local ${game} inis`);
        }
    }

    return profileFolder;
}

export async function getMO2ProfileSettingsData(settingsIniPath: string): Promise<INIData> {
    const settingsIniData = await ParseIniFile(settingsIniPath);
    if (
        !settingsIniData ||
        settingsIniData.General === undefined ||
        settingsIniData.General.LocalSettings === undefined
    ) {
        throw new Error(`MO2 profile Settings ini file ${settingsIniPath} is not parsable`);
    }
    return settingsIniData;
}

export async function isMO2Running() {
    return (await getPIDforProcessName(MO2Lib.MO2EXEName)).length > 0;
}
export async function isMO2ButNotThisOneRunning(MO2EXEPath: string) {
    const pids = await getPIDforProcessName(MO2Lib.MO2EXEName);
    if (pids.length === 0) {
        return false;
    }
    const ourPids = await getPIDsforFullPath(MO2EXEPath);
    if (ourPids.length === 0) {
        return true;
    }
    return pids.some((pid) => ourPids.indexOf(pid) === -1);
}
export async function isOurMO2Running(MO2EXEPath: string) {
    return (await getPIDsforFullPath(MO2EXEPath)).length > 0;
}

export async function killAllMO2Processes() {
    const pids = await getPIDforProcessName(MO2Lib.MO2EXEName);
    if (pids.length > 0) {
        pids.map((pid) => process.kill(pid));
    }
}
