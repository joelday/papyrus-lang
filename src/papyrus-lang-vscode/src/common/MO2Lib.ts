import { existsSync, openSync, readdirSync, readFileSync, writeFileSync } from 'fs';
import * as fs from 'fs';
import path from 'path';
import { INIData, ParseIniArray, ParseIniFile, SerializeIniArray } from './INIHelpers';
import { getLocalAppDataFolder, getRegistryValueData } from './OSHelpers';

export enum ModEnabledState {
    unmanaged = '*',
    enabled = '+',
    disabled = '-',
    unknown = '?',
}

export class ModListItem {
    public name: string = '';
    public enabled: ModEnabledState = ModEnabledState.unmanaged;
    constructor(name: string, enabled: ModEnabledState) {
        this.name = name;
        this.enabled = enabled;
    }
}

export const InstanceIniName = 'ModOrganizer.ini';
export const MO2EXEName = 'ModOrganizer.exe';

export interface MO2CustomExecutableInfo {
    arguments: string;
    binary: string;
    hide: boolean;
    ownicon: boolean;
    steamAppID: string;
    title: string;
    toolbar: boolean;
    workingDirectory: string;
}

// TODO: Starfield: Verify values
export type MO2LongGameID =
    | 'Fallout 4'
    | 'Skyrim Special Edition'
    | 'Skyrim'
    | 'Starfield'
    | 'Enderal'
    | 'Fallout 3'
    | 'Fallout 4 VR'
    | 'New Vegas'
    | 'Morrowind'
    | 'Skyrim VR'
    | 'TTW'
    | 'Other';
export type MO2ShortGameID = 'Fallout4' | 'SkyrimSE' | 'Skyrim' | 'Starfield'; //TODO: Do the rest of these | 'enderal' | 'fo3' | 'fo4vr' | 'nv' | 'morrowind' | 'skyrimvr' | 'ttw' | 'other';

export interface MO2InstanceInfo {
    name: string;
    gameName: MO2LongGameID;
    gameDirPath: string;
    selectedProfile: string;
    iniPath: string;
    baseDirectory: string;
    downloadsFolder: string;
    modsFolder: string;
    cachesFolder: string;
    profilesFolder: string;
    overwriteFolder: string;
    customExecutables: MO2CustomExecutableInfo[];
}

// import a library that deals with nukpg version strings

// import the semver library

export interface WinVerObject {
    major: number;
    minor: number;
    build: number;
    privateNum: number;
    version: string;
}

export class WinVer implements WinVerObject {
    public readonly major: number = 0;
    public readonly minor: number = 0;
    public readonly build: number = 0;
    public readonly privateNum: number = 0;
    public readonly version: string = '0.0.0.0';
    public static fromVersionString(version: string): WinVer {
        const parts = version.split('.');
        const major = parseInt(parts[0]);
        const minor = parseInt(parts[1]);
        const build = parseInt(parts[2]);
        const privateNum = parseInt(parts[3]);
        return new WinVer({ major, minor, build, privateNum, version });
    }
    public static lessThan(a: WinVerObject, b: WinVerObject): boolean {
        return a.major < b.major || a.minor < b.minor || a.build < b.build || a.privateNum < b.privateNum;
    }
    public static equal(a: WinVerObject, b: WinVerObject): boolean {
        return a.major === b.major && a.minor === b.minor && a.build === b.build && a.privateNum === b.privateNum;
    }
    public static greaterThan(a: WinVerObject, b: WinVerObject): boolean {
        return a.major > b.major || a.minor > b.minor || a.build > b.build || a.privateNum > b.privateNum;
    }
    public static greaterThanOrEqual(a: WinVerObject, b: WinVerObject): boolean {
        return WinVer.greaterThan(a, b) || WinVer.equal(a, b);
    }
    public lt(other: WinVer): boolean {
        return WinVer.lessThan(this, other);
    }
    public eq(other: WinVer): boolean {
        return WinVer.equal(this, other);
    }
    public gt(other: WinVer): boolean {
        return WinVer.greaterThan(this, other);
    }
    constructor(iWinVer: WinVerObject) {
        this.major = iWinVer.major;
        this.minor = iWinVer.minor;
        this.build = iWinVer.build;
        this.privateNum = iWinVer.privateNum;
        this.version = iWinVer.version;
    }
}

export interface MO2ModMetaInstalledFile {
    modid: number;
    fileid: number;
}

export interface MO2ModMeta {
    modid: number;
    version: string;
    newestVersion: string;
    category: string;
    installedFiles: MO2ModMetaInstalledFile[];
    nexusFileStatus?: number;
    gameName?: MO2ShortGameID;
    ignoredVersion?: string;
    installationFile?: string;
    repository?: string;
    comments?: string;
    notes?: string;
    nexusDescription?: string;
    url?: string;
    hasCustomURL?: boolean;
    lastNexusQuery?: string;
    lastNexusUpdate?: string;
    nexusLastModified?: string;
    converted?: boolean;
    validated?: boolean;
    color?: string;
    endorsed?: number;
    tracked?: number;
}

export interface MO2Location {
    MO2EXEPath: string;
    instances: MO2InstanceInfo[];
    isPortable: boolean;
}

export function GetGlobalMO2DataFolder(): string | undefined {
    const appdata = getLocalAppDataFolder();
    if (appdata === undefined) {
        return undefined;
    }
    return path.join(appdata, 'ModOrganizer');
}

export async function IsMO2Portable(MO2EXEPath: string): Promise<boolean> {
    const basedir = path.dirname(MO2EXEPath);
    const portableSigil = path.join(basedir, 'portable.txt');
    if (existsSync(portableSigil)) {
        return true;
    }
    return false;
}

export async function GetMO2EXELocations(gameId?: MO2LongGameID, ...additionalIds: MO2LongGameID[]): Promise<string[]> {
    let possibleLocations: string[] = [];
    const nxmHandlerIniPath = await FindNXMHandlerIniPath();
    if (nxmHandlerIniPath === undefined) {
        return possibleLocations;
    }
    const MO2NXMData = await ParseIniFile(nxmHandlerIniPath);
    if (MO2NXMData === undefined) {
        return possibleLocations;
    }
    possibleLocations = GetMO2EXELocationsFromNXMHandlerData(MO2NXMData, gameId, ...additionalIds);
    // Filter all the ones that don't exist
    return possibleLocations.filter((value) => fs.existsSync(value));
}

function getIDFromNXMHandlerName(nxmName: string): MO2LongGameID | undefined {
    const _nxmName = nxmName.toLowerCase().replace(/ /g, '');
    switch (_nxmName) {
        case 'starfield':
            return 'Starfield';
        case 'skyrimse':
        case 'skyrimspecialedition':
            return 'Skyrim Special Edition';
        case 'skyrim':
            return 'Skyrim';
        case 'fallout4':
            return 'Fallout 4';
        case 'enderal':
            return 'Enderal';
        case 'fallout3':
            return 'Fallout 3';
        case 'fallout4vr':
            return 'Fallout 4 VR';
        case 'falloutnv':
        case 'newvegas':
            return 'New Vegas';
        case 'morrowind':
            return 'Morrowind';
        case 'skyrimvr':
            return 'Skyrim VR';
        case 'ttw':
            return 'TTW';
        case 'other':
            return 'Other';
        default:
            return undefined;
    }
}

/**
 * ModOrganizer2 installs a nxmhandler.ini file in the global data folder.
 * This conveniently has a list of all the MO2 installations (even the portable ones)
 * and their associated game(s).
 *
 * @param nxmData
 * @param gameID - The game ID to filter by
 * @param additionalIds - Additional game IDs to filter by
 */
function GetMO2EXELocationsFromNXMHandlerData(
    nxmData: INIData,
    gameId?: MO2LongGameID,
    ...additionalIds: MO2LongGameID[]
): string[] {
    let exePaths: string[] = [];
    if (!nxmData.handlers) {
        return exePaths;
    }
    const handler_array = ParseIniArray(nxmData.handlers);
    if (!handler_array || handler_array.length === 0) {
        return exePaths;
    }
    for (const handler of handler_array) {
        const executable: string | undefined = handler.executable;
        const games: string | undefined = handler.games;
        if (!executable || !games) {
            continue;
        }
        const gameList = NormalizeIniString(games)
            .split(',')
            .filter((val) => val !== '');
        if (!gameId) {
            exePaths.push(executable);
        } else {
            const _args = [gameId, ...additionalIds];
            for (const gameID of _args) {
                if (
                    gameList.filter((val) => {
                        const _valID = getIDFromNXMHandlerName(val);
                        if (_valID === undefined) {
                            return false;
                        }
                        return _valID === gameID;
                    }).length > 0
                ) {
                    exePaths.push(executable);
                    break;
                }
            }
        }
    }

    // filter out non-uniques
    // We do it after the above because duplicate paths may have different `games` values
    exePaths = exePaths.filter((value, index, self) => {
        return self.indexOf(value) === index;
    });
    return exePaths;
}
function NormalizeIniString(str: string) {
    let _str = str;
    if (_str.startsWith('@ByteArray(') && _str.endsWith(')')) {
        _str = _str.substring(11, _str.length - 1);
    }
    // replace all '\"' with '"'
    _str = _str.replace(/\\"/g, '"');
    // replace all '\\' with '\'
    _str = _str.replace(/\\\\/g, '\\');
    return _str;
}
function NormalizeIniPathString(pathstring: string) {
    let _str = NormalizeIniString(pathstring);
    // remove all leading and trailing quotes
    if (_str.startsWith('\\"') && _str.endsWith('\\"')) {
        _str = _str.substring(2, _str.length - 2);
    }
    if (_str.startsWith('"') && _str.endsWith('"')) {
        _str = _str.substring(1, _str.length - 1);
    }
    return _normalizePath(_str);
}
function NormalizeMO2IniPathString(pathstring: string, basedir: string) {
    return _normalizePath(NormalizeIniPathString(pathstring)?.replace(/%BASE_DIR%/g, _normalizePath(basedir) + '/'));
}
function NormalizePath(pathstring: string): string {
    return _normalizePath(pathstring) || '';
}
function _normalizePath(pathstring: string | undefined) {
    return pathstring === undefined ? undefined : path.normalize(pathstring).replace(/\\/g, '/');
}

function _normInistr(str: string | undefined): string | undefined {
    return str === undefined ? undefined : NormalizeIniString(str);
}

function _normIniPath(pathstring: string | undefined): string | undefined {
    return pathstring === undefined ? undefined : NormalizeIniPathString(pathstring);
}

function _normMO2Path(pathstring: string | undefined, basedir: string | undefined): string | undefined {
    return pathstring === undefined || basedir === undefined
        ? undefined
        : NormalizeMO2IniPathString(pathstring, basedir);
}

function ParseMO2CustomExecutable(iniData: INIData) {
    const title = _normInistr(iniData.title);
    const binary = _normIniPath(iniData.binary);
    const steamAppID = _normInistr(iniData.steamAppID) || '';
    const toolbar = iniData.toolbar === true; // explicit boolean check in case unset
    const hide = iniData.hide === true;
    const ownicon = iniData.ownicon === true;
    const arguments_ = _normInistr(iniData.arguments) || '';
    const workingDirectory = _normIniPath(iniData.workingDirectory) || '';
    if (title !== undefined && binary !== undefined) {
        const result: MO2CustomExecutableInfo = {
            arguments: arguments_,
            binary: binary,
            hide: hide,
            ownicon: ownicon,
            steamAppID: steamAppID,
            title: title,
            toolbar: toolbar,
            workingDirectory: workingDirectory,
        };
        return result;
    }
    return undefined;
}

function ParseMO2CustomExecutables(iniArray: INIData[]) {
    const result: MO2CustomExecutableInfo[] = [];
    for (const iniData of iniArray) {
        const parsed = ParseMO2CustomExecutable(iniData);
        if (parsed) {
            result.push(parsed);
        }
    }
    return result;
}

/**
 * Parses the data from a ModOrganizer.ini file and returns the information needed
 *
 * The ini data is structured like this:
 * ```none
 * [General]
 * gameName=Skyrim Special Edition
 * selected_profile=@ByteArray(Default)
 * gamePath=@ByteArray(D:\\Games\\Skyrim Special Edition)
 * version=2.4.4
 * first_start=false
 * <...>
 * [Settings]
 * <...>
 * base_directory="D:\\ModsForSkyrim"
 * mod_directory=D:\ModsForSkyrim\mods
 * ```
 *
 * gameName is the long MO2 identifier of the game, like "Skyrim Special Edition"; it's set by MO2, it's not possible to be changed by the user
 *
 * base_directory doesn't get set if the ModOrganizer.ini file is in the base directory
 *
 * the various _directory values don't get set if they do not differ from %BASE_DIR%/{value}
 *
 * @param iniPath - path to the ModOrganizer.ini file
 * @param iniData - data from the ModOrganizer.ini file
 * @returns
 */
function ParseInstanceINI(iniPath: string, iniData: INIData, isPortable: boolean): MO2InstanceInfo | undefined {
    const iniBaseDir = NormalizePath(path.dirname(iniPath));
    const instanceName = isPortable ? 'portable' : path.basename(iniBaseDir);
    const gameName: MO2LongGameID = iniData.General['gameName'];
    if (gameName === undefined) {
        return undefined;
    }
    const gameDirPath = _normIniPath(iniData.General['gamePath']);
    if (gameDirPath === undefined) {
        return undefined;
    }
    // TODO: We should probably pin to a specific minor version of MO2
    const _version = iniData.General['version'];

    // TODO: Figure out if this is ever not set
    const selectedProfile = _normInistr(iniData.General['selected_profile']) || 'Default';

    const settings = iniData.Settings || {}; // Settings may be empty; we don't need it to populate the rest of the information

    const baseDirectory = _normIniPath(settings['base_directory']) || iniBaseDir;
    const downloadsPath =
        _normMO2Path(settings['download_directory'], baseDirectory) || path.join(baseDirectory, 'downloads');
    const modsPath = _normMO2Path(settings['mod_directory'], baseDirectory) || path.join(baseDirectory, 'mods');
    const cachesPath = _normMO2Path(settings['cache_directory'], baseDirectory) || path.join(baseDirectory, 'webcache');
    const profilesPath =
        _normMO2Path(settings['profiles_directory'], baseDirectory) || path.join(baseDirectory, 'profiles');
    const overwritePath =
        _normMO2Path(settings['overwrite_directory'], baseDirectory) || path.join(baseDirectory, 'overwrite');
    let customExecutables: MO2CustomExecutableInfo[] = [];
    if (iniData.customExecutables) {
        const arr = ParseIniArray(iniData.customExecutables);
        if (arr && arr.length > 0) {
            customExecutables = ParseMO2CustomExecutables(arr);
        }
    }
    return {
        name: instanceName,
        gameName: gameName,
        gameDirPath: gameDirPath,
        customExecutables: customExecutables,
        selectedProfile: selectedProfile,
        iniPath: iniPath,
        baseDirectory: baseDirectory,
        downloadsFolder: downloadsPath,
        modsFolder: modsPath,
        cachesFolder: cachesPath,
        profilesFolder: profilesPath,
        overwriteFolder: overwritePath,
    };
}

/**
 * Parses the data from a ModOrganizer.ini file and returns an MO2InstanceInfo object
 * @param iniPath - path to the ModOrganizer.ini file
 * @returns MO2InstanceInfo
 */
export async function GetMO2InstanceInfo(iniPath: string): Promise<MO2InstanceInfo | undefined> {
    const iniData = await ParseIniFile(iniPath);
    if (iniData === undefined) {
        return undefined;
    }
    return ParseInstanceINI(iniPath, iniData, await IsMO2Portable(iniPath));
}

export async function validateInstanceLocationInfo(info: MO2InstanceInfo): Promise<boolean> {
    // check that all the directory paths exist and they are directories
    const dirPaths = [
        info.gameDirPath,
        info.baseDirectory,
        info.downloadsFolder,
        info.modsFolder,
        info.cachesFolder,
        info.profilesFolder,
        info.overwriteFolder,
    ];
    for (const p of dirPaths) {
        if (!existsSync(p)) {
            return false;
        }
        //check if p is a directory
        if (!fs.statSync(p).isDirectory()) {
            return false;
        }
    }
    if (!existsSync(info.iniPath) || !fs.statSync(info.iniPath).isFile()) {
        return false;
    }
    return true;
}

export async function FindInstanceForEXE(MO2EXEPath: string, instanceName?: string) {
    if (!fs.existsSync(MO2EXEPath)) {
        return undefined;
    }
    const isPortable = await IsMO2Portable(MO2EXEPath);
    if (isPortable) {
        const instanceFolder = path.dirname(MO2EXEPath);
        const instanceIniPath = path.join(instanceFolder, InstanceIniName);
        return await GetMO2InstanceInfo(instanceIniPath);
    } else if (instanceName !== undefined && instanceName !== 'portable') {
        return await FindGlobalInstance(instanceName);
    }
    return undefined;
}

function _portableExeIni(exePath: string): string {
    return path.join(path.dirname(exePath), InstanceIniName);
}

export async function GetLocationInfoForEXE(
    MO2EXEPath: string,
    gameId?: MO2LongGameID,
    ...addtionalIds: MO2LongGameID[]
): Promise<MO2Location | undefined> {
    if (!fs.existsSync(MO2EXEPath)) {
        return undefined;
    }
    const isPortable = await IsMO2Portable(MO2EXEPath);
    let instanceInfos: MO2InstanceInfo[] = [];
    if (isPortable) {
        const instanceInfo = await GetMO2InstanceInfo(_portableExeIni(MO2EXEPath));
        instanceInfos = instanceInfo ? [instanceInfo] : [];
    } else {
        instanceInfos = await FindGlobalInstances(gameId, ...addtionalIds);
    }
    if (instanceInfos.length === 0) {
        return undefined;
    }
    return {
        MO2EXEPath: MO2EXEPath,
        isPortable: isPortable,
        instances: instanceInfos,
    };
}

export async function FindAllKnownMO2EXEandInstanceLocations(
    gameID?: MO2LongGameID,
    ...additionalIds: MO2LongGameID[]
): Promise<MO2Location[]> {
    const possibleLocations: MO2Location[] = [];
    const exeLocations = await GetMO2EXELocations(gameID, ...additionalIds);
    if (exeLocations.length !== 0) {
        const globalInstances = (await FindGlobalInstances(gameID)) || [];
        for (const exeLocation of exeLocations) {
            let instanceInfos: MO2InstanceInfo[] | undefined = undefined;
            const isPortable = await IsMO2Portable(exeLocation);
            if (isPortable) {
                const instanceInfo = await GetMO2InstanceInfo(_portableExeIni(exeLocation));
                instanceInfos = instanceInfo ? [instanceInfo] : [];
            } else {
                instanceInfos = globalInstances;
            }
            if (instanceInfos.length === 0) {
                continue;
            }
            possibleLocations.push({
                MO2EXEPath: exeLocation,
                instances: instanceInfos,
                isPortable: isPortable,
            });
        }
    }
    return possibleLocations;
}

function GetNXMHandlerIniPath(): string | undefined {
    const global = GetGlobalMO2DataFolder();
    if (global === undefined) {
        return undefined;
    }
    return path.join(global, 'nxmhandler.ini');
}

export async function FindNXMHandlerIniPath(): Promise<string | undefined> {
    const nxmHandlerIniPath = GetNXMHandlerIniPath();
    if (nxmHandlerIniPath === undefined || !existsSync(nxmHandlerIniPath)) {
        return undefined;
    }
    return nxmHandlerIniPath;
}

export async function IsInstanceOfGame(gameID: MO2LongGameID, instanceIniPath: string): Promise<boolean> {
    const iniData = await ParseIniFile(instanceIniPath);
    if (iniData === undefined) {
        return false;
    }
    return _isInstanceOfGames(iniData, gameID);
}

function _isInstanceOfGames(
    instanceIniData: INIData,
    gameID: MO2LongGameID,
    ...additionalIds: MO2LongGameID[]
): boolean {
    if (instanceIniData.General === undefined || instanceIniData.General.gameName === undefined) {
        return false;
    }

    const gameIDs = [gameID, ...additionalIds];
    for (const id of gameIDs) {
        if (instanceIniData.General.gameName === id) {
            return true;
        }
    }
    return false;
}

export async function FindGlobalInstance(name: string): Promise<MO2InstanceInfo | undefined> {
    const globalFolder = GetGlobalMO2DataFolder();
    if (globalFolder === undefined || (!existsSync(globalFolder) && !fs.statSync(globalFolder).isDirectory())) {
        return undefined;
    }
    const instanceNames = readdirSync(globalFolder, { withFileTypes: true });
    const instance = instanceNames.find((dirent) => dirent.isDirectory() && dirent.name === name);
    if (instance === undefined) {
        return undefined;
    }
    const instanceIniPath = path.join(globalFolder, instance.name, InstanceIniName);
    const iniData = await ParseIniFile(instanceIniPath);
    if (!iniData) {
        return undefined;
    }
    return ParseInstanceINI(instanceIniPath, iniData, false);
}

export async function FindGlobalInstances(
    gameId?: MO2LongGameID,
    ...additionalIds: MO2LongGameID[]
): Promise<MO2InstanceInfo[]> {
    const possibleLocations: MO2InstanceInfo[] = [];
    const globalFolder = GetGlobalMO2DataFolder();
    // list all the directories in globalMO2Data
    if (globalFolder === undefined || (!existsSync(globalFolder) && !fs.statSync(globalFolder).isDirectory())) {
        return [];
    }
    const instanceNames = readdirSync(globalFolder, { withFileTypes: true });
    for (const dirent of instanceNames) {
        if (dirent.isDirectory()) {
            const instanceIniPath = path.join(globalFolder, dirent.name, InstanceIniName);
            const iniData = await ParseIniFile(instanceIniPath);
            if (!iniData) {
                continue;
            }
            if (gameId !== undefined && !_isInstanceOfGames(iniData, gameId, ...additionalIds)) {
                continue;
            }
            const info = ParseInstanceINI(instanceIniPath, iniData, false);
            if (info !== undefined) {
                possibleLocations.push(info);
            }
        }
    }
    return possibleLocations;
}

export async function GetCurrentGlobalInstance(): Promise<MO2InstanceInfo | undefined> {
    // HKEY_CURRENT_USER\SOFTWARE\Mod Organizer Team\Mod Organizer\CurrentInstance
    const currentInstanceName = await getRegistryValueData(
        'SOFTWARE\\Mod Organizer Team\\Mod Organizer',
        'CurrentInstance',
        'HKCU'
    );
    if (currentInstanceName) {
        return FindGlobalInstance(currentInstanceName);
    }
    return undefined;
}

/**
 * Parse modlist.txt file contents from Mod Organizer 2
 *
 * The format is:
 * - Mod names are the names of their directories in the mods folder
 *   i.e. "SkyUI", not "SkyUI.esp", with an exception for official DLC, which is prefixed with "DLC: "
 * - Comments are prefixed with '#'
 * - Enabled mods are prefixed with "+" and disabled mods are prefixed with "-"
 * - Unmanaged mods (e.g. Game DLC) are prefixed with "*"
 * - Seperators are prefixed with "-" and have the suffix "_separator"
 * - The mods are loaded in order.
 * - Any mods listed earlier will overwrite files in mods listed later.
 * They appear in the ModOrganizer gui in reverse order (i.e. the last mod in the file is the first mod in the gui)
 *
 * Example of a modlist.txt file:
 * ```none
 * # This file was automatically generated by Mod Organizer.
 * +Unofficial Skyrim Special Edition Patch
 * -SkyUI
 * +Immersive Citizens - AI Overhaul
 * *DLC: Automatron
 * +Immersive Citizens - OCS patch
 * -Auto Loot SE
 * *DLC: Far Harbor
 * *DLC: Contraptions Workshop
 * ```
 * This function returns an array of mod list items in the order they appear in the file
 */

export function ParseModListText(modListContents: string): ModListItem[] {
    const modlist = new Array<ModListItem>();
    const modlistLines = modListContents.replace(/\r\n/g, '\n').split('\n');
    for (const line of modlistLines) {
        if (line.charAt(0) === '#' || line === '') {
            continue;
        }
        const indic = line.charAt(0);
        const modName = line.substring(1);
        let modEnabledState: ModEnabledState | undefined = undefined;
        switch (indic) {
            case '+':
                modEnabledState = ModEnabledState.enabled;
                break;
            case '-':
                modEnabledState = ModEnabledState.disabled;
                break;
            case '*':
                modEnabledState = ModEnabledState.unmanaged;
                break;
        }
        if (modEnabledState === undefined) {
            // skip this line
            continue;
        }
        modlist.push(new ModListItem(modName, modEnabledState));
    }
    return modlist;
}

export async function ParseModListFile(modlistPath: string): Promise<ModListItem[] | undefined> {
    // create an ordered map of mod names to their enabled state
    if (!fs.existsSync(modlistPath) || !fs.lstatSync(modlistPath).isFile()) {
        return undefined;
    }
    const modlistContents = readFileSync(modlistPath, 'utf8').replace(/\r\n/g, '\n');
    if (!modlistContents) {
        return undefined;
    }
    return ParseModListText(modlistContents);
}
// parse moshortcut URI

export function parseMoshortcutURI(moshortcutURI: string): { instanceName: string; exeName: string } {
    const moshortcutparts = moshortcutURI.replace('moshortcut://', '').split(':');
    const instanceName = moshortcutparts[0] || 'portable';
    const exeName = moshortcutparts[1];
    return { instanceName, exeName };
}

export function checkIfModExistsAndEnabled(modlist: Array<ModListItem>, modName: string) {
    return modlist.findIndex((mod) => mod.name === modName && mod.enabled === ModEnabledState.enabled) !== -1;
}

/**
 * If we find the mod in the modlist, remove it and return the new modlist
 * @param modlist
 * @param modName
 * @returns
 */

export function IndexOfModList(modlist: Array<ModListItem>, modName: string) {
    return modlist.findIndex((m) => m.name === modName);
}

export function RemoveMod(modlist: Array<ModListItem>, modName: string) {
    const modIndex = modlist.findIndex((m) => m.name === modName);
    if (modIndex !== -1) {
        return modlist.slice(0, modIndex).concat(modlist.slice(modIndex + 1));
    }
    return modlist;
}

export function AddModToBeginningOfModList(modlist: Array<ModListItem>, mod: ModListItem) {
    // check if the mod is already in the modlist
    const modIndex = modlist.findIndex((m) => m.name === mod.name);
    if (modIndex !== -1) {
        // if the mod is already in the modlist, remove it and return the modlist with the specified mod at the top
        return [mod].concat(modlist.slice(0, modIndex).concat(modlist.slice(modIndex + 1)));
    }
    return [mod].concat(modlist);
}

export function AddModIfNotInModList(modlist: Array<ModListItem>, mod: ModListItem) {
    // check if the mod is already in the modlist
    const modIndex = IndexOfModList(modlist, mod.name);
    if (modIndex === -1) {
        // if the mod is not already in the modlist, add it at the beginning
        return [mod].concat(modlist);
    }
    // otherwise just return it
    return modlist;
}

export function AddOrEnableModInModList(modlist: Array<ModListItem>, modName: string) {
    const modIndex = modlist.findIndex((m) => m.name === modName);
    if (modIndex !== -1) {
        return modlist
            .slice(0, modIndex)
            .concat(new ModListItem(modName, ModEnabledState.enabled), modlist.slice(modIndex + 1));
    }
    return AddModToBeginningOfModList(modlist, new ModListItem(modName, ModEnabledState.enabled));
}

// modlist.txt has to be in CRLF, because MO2 is cursed
export function ModListToText(modlist: Array<ModListItem>) {
    let modlistText = '# This file was automatically generated by Mod Organizer.\r\n';
    for (const mod of modlist) {
        modlistText += mod.enabled + mod.name + '\r\n';
    }
    return modlistText;
}

export function WriteChangesToModListFile(modlistPath: string, modlist: Array<ModListItem>) {
    const modlistContents = ModListToText(modlist);
    // fs.rmSync(modlistPath, { force: true });
    if (!openSync(modlistPath, 'w')) {
        return false;
    }
    writeFileSync(modlistPath, modlistContents, 'utf8');
    return true;
}

/**
 * MO2 handles exe arguments awfully, which is why we have to do this tortured parsing.
 * Don't use this for anything other than MO2, because it's not a general purpose parser
 * Note: This is not used for parsing the custom executable objects; args are stored as the literal there
 *
 * In MO2, this is just a string, and the only argument passed to an executable is that string, instead of as an array of arguments.
 * cmd.exe ends up mangling the arguments if they contain quote-literals.
 */
export function ParseMO2CmdLineArguments(normargstring: string) {
    const args: string[] = [];
    let arg = '';
    let inQuote = false;
    for (let i = 0; i < normargstring.length; i++) {
        const char = normargstring[i];
        // if we hit a space, and we're not in a quote, then we've hit the end of an argument
        if (char === ' ') {
            if (!inQuote && arg.length > 0) {
                args.push(arg);
                arg = '';
            } else {
                arg += char;
            }
        } else if (char === '"') {
            // if we hit a quote, and we're not in a quote, then we're starting a quote
            if (inQuote === false) {
                // If the arg started, add the quote to it
                if (arg.length > 0) {
                    arg += char;
                }
                // Even if the above is true, we're still starting a quote
                inQuote = true;
            } else {
                // if we hit a quote, and we're in a quote, then we're ending a quote
                inQuote = false;
                // peek ahead to see if the next character is a space
                if (i !== normargstring.length - 1 && normargstring[i + 1] !== ' ') {
                    arg += char;
                }

                // if the argument already has a quote literal in it, then we need to add the quote to the arg
                else if (arg.indexOf('"') !== -1) {
                    arg += char;
                }
            }
        } else {
            arg += char;
        }
    }
    // get the last one if there was one
    if (arg.length > 0 && arg.trim() !== '') {
        args.push(arg);
    }
    return args;
}
/***
 * Format is like this:
 * ```none
 * [General]
 * gameName=Fallout4
 * modid=47327
 * ignoredVersion=
 * version=1.10.163.0
 * newestVersion=1.10.163.0
 * category="35,"
 * nexusFileStatus=1
 * installationFile=Addres Library-47327-1-10-163-0-1599728753.zip
 * repository=Nexus
 * comments=
 * notes=
 * nexusDescription="This project is a resource for plugins developed using [url=https://github.com/Ryan-rsm-McKenzie/CommonLibF4]CommonLibF4.[/url]"
 * url=
 * hasCustomURL=false
 * lastNexusQuery=2022-12-23T00:08:20Z
 * lastNexusUpdate=2022-12-23T00:08:20Z
 * nexusLastModified=2020-09-10T09:08:54Z
 * converted=false
 * validated=false
 * color=@Variant(\0\0\0\x43\0\xff\xff\0\0\0\0\0\0\0\0)
 * endorsed=0
 * tracked=0
 *
 * [installedFiles]
 * 1\modid=47327
 * 1\fileid=191018
 * size=1
 * ```
 *
 * the only required fields are:
 * - modid
 * - version
 * - newestVersion
 * - category
 * - installationFile
 * - [installedFiles].size
 */

export function isKeyOfObject<T extends object>(key: string | number | symbol, obj: T): key is keyof T {
    return key in obj;
}

function ParseModMetaIni(modMetaIni: INIData): MO2ModMeta | undefined {
    if (!modMetaIni) {
        return undefined;
    }
    if (modMetaIni.farts === undefined) {
        console.log('lmao');
    }
    if (
        modMetaIni.General === undefined ||
        modMetaIni.General.modid === undefined ||
        modMetaIni.General.version === undefined ||
        modMetaIni.General.newestVersion === undefined ||
        modMetaIni.General.installationFile === undefined ||
        modMetaIni.General.category === undefined ||
        modMetaIni.installedFiles === undefined ||
        modMetaIni.installedFiles.size === undefined
    ) {
        return undefined;
    }
    const general = modMetaIni.General;
    // check if each key in general is a key in the type MO2ModMeta
    // TODO: figure out how to do this without the any
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const modMeta = {} as any;
    for (const key in general) {
        if (isKeyOfObject(key, general as MO2ModMeta)) {
            modMeta[key] = general[key];
        }
    }
    const installedFilesSize = modMetaIni.installedFiles.size;
    if (!installedFilesSize) {
        return undefined;
    }
    const installedFiles = ParseIniArray(modMetaIni.installedFiles);
    if (!installedFiles) {
        return undefined;
    }
    modMeta['installedFiles'] = installedFiles.map((installedFile) => {
        return {
            modid: installedFile.modid,
            fileid: installedFile.fileid,
        } as MO2ModMetaInstalledFile;
    });
    return modMeta as MO2ModMeta;
}

export function SerializeModMetaInfo(info: MO2ModMeta) {
    const ini = {} as INIData;
    ini.General = {} as INIData;
    Object.keys(info).forEach((key) => {
        if (key !== 'installedFiles' && info[key as keyof MO2ModMeta] !== undefined) {
            ini.General[key] = info[key as keyof MO2ModMeta];
        }
    });
    ini.installedFiles = SerializeIniArray(info.installedFiles);
    return ini;
}

export async function ParseModMetaIniFile(modMetaIniPath: string) {
    const modMetaIni = await ParseIniFile(modMetaIniPath);
    if (!modMetaIni) {
        return undefined;
    }
    return ParseModMetaIni(modMetaIni);
}

export function AddSeparatorToBeginningOfModList(name: string, modList: ModListItem[]): ModListItem[] {
    return AddModIfNotInModList(modList, new ModListItem(name + '_separator', ModEnabledState.disabled));
}
