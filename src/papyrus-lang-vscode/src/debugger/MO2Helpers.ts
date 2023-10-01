import { existsSync, openSync, readFileSync, writeFileSync } from 'fs';
import path from 'path';
import { getRegistryKeyForGame, getScriptExtenderName } from "../PapyrusGame";
import { PapyrusGame } from "../PapyrusGame";


import * as ini from 'ini';

import {
    AddressLibraryF4SEModName,
    AddressLibraryName,
    AddressLibrarySKSEAEModName,
    AddressLibrarySKSEModName,
    PDSModName,
} from '../common/constants';

export interface INIData{
    [key: string]: any;
}
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

export function getGameIniName(game: PapyrusGame): string {
    return game == PapyrusGame.fallout4 ? 'fallout4.ini' :'skyrim.ini';
}

export function getGameIniPath(profilePath: string, game: PapyrusGame): string {
    let iniName = getGameIniName(game);
    return path.join(profilePath, iniName);
}

// TODO: Refactor these "Find" functions into PathResolver
export function GetGlobalGameSavedDataFolder(game: PapyrusGame){
    if (process.env.HOMEPATH === undefined) {
        return undefined;
    }
    return path.join(process.env.HOMEPATH, "Documents", "My Games", getRegistryKeyForGame(game));
}

export function GetLocalAppDataFolder(){
    return process.env.LOCALAPPDATA;
}

export async function FindMO2InstanceIniPath(modsFolder: string, MO2EXEPath: string, instanceName: string | undefined){
    // check if the instance ini is in the same folder as the mods folder
    let instanceIniPath = path.join(path.dirname(modsFolder), 'ModOrganizer.ini');
    if (!existsSync(instanceIniPath)) {
        // check if it's a portable instance
        let portableSigil = path.join(path.dirname(MO2EXEPath), 'portable.txt');
        // if it's not a portable instance, check appdata
        if(!existsSync(portableSigil)){
          if (!instanceName) {
            return undefined;
          }
          let appdata = GetLocalAppDataFolder();
          if (appdata === undefined) {
            return undefined;
          }
          instanceIniPath = path.join(appdata, 'ModOrganizer', instanceName, 'ModOrganizer.ini');
        } else {
            if (instanceName === "portable") {
                // we shouldn't be here
                throw new Error("FUCK!");
            }
          // portable instance, instance ini should be in the same dir as the exe
          instanceIniPath = path.join(path.dirname(MO2EXEPath), 'ModOrganizer.ini');
        }
    }
    if (!existsSync(instanceIniPath)) {
      return undefined;
    }  

    return instanceIniPath;
}

export async function FindMO2ProfilesFolder(modsFolder: string, MO2InstanceIniData : INIData): Promise<string | undefined> {
  let parentFolder = path.dirname(modsFolder);
  let profilesFolder = path.join(parentFolder, 'profiles');
  if (existsSync(profilesFolder)) {
      return profilesFolder;
  }
  if (!MO2InstanceIniData) {
    return undefined;
  }
  // if it's not there, then we have to check the Mod Organizer 2 instance ini file
  profilesFolder = MO2InstanceIniData.Settings.profiles_directory;
  if (!profilesFolder) {
      return undefined;
  }
  if (profilesFolder.startsWith('%BASE_DIR%')) {
      profilesFolder = path.join(parentFolder, profilesFolder.substring(10).trim());
  } else if (!path.isAbsolute(profilesFolder)) {
      profilesFolder = path.join(parentFolder, profilesFolder);
  }
  if (existsSync(profilesFolder)) {
      return profilesFolder;
  }
  return undefined;
}

export function getAddressLibNames(game: PapyrusGame): AddressLibraryName[] {
  if (game === PapyrusGame.fallout4) {
      return [AddressLibraryF4SEModName];
  } else if (game === PapyrusGame.skyrimSpecialEdition) {
    return [AddressLibrarySKSEModName, AddressLibrarySKSEAEModName];
  }
  throw new Error("ERROR: Unsupported game!");
}

export async function ParseIniFile(IniPath: string): Promise <INIData | undefined> {
    let IniText = readFileSync(IniPath, 'utf-8');
    if (!IniText) {
        return undefined;
    }
    let IniObject = ini.parse(readFileSync(IniPath, 'utf-8'));
    return IniObject as INIData;
}

export function CheckIfDebuggingIsEnabledInIni(skyrimIni: INIData) {
    return (
        skyrimIni.Papyrus.bLoadDebugInformation === 1 &&
        skyrimIni.Papyrus.bEnableTrace === 1 &&
        skyrimIni.Papyrus.bEnableLogging === 1
    );
}

export function TurnOnDebuggingInIni(skyrimIni: INIData) {
    const _ini = skyrimIni;
    _ini.Papyrus.bLoadDebugInformation = 1;
    _ini.Papyrus.bEnableTrace = 1;
    _ini.Papyrus.bEnableLogging = 1;
    return _ini;
}


/** Parse modlist.txt file contents from Mod Organizer 2
 *
 * The format is:
 * comments are prefixed with '#'
 * mod names are the names of their directories in the mods folder
 * enabled mods are prefixed with "+" and disabled mods are prefixed with "-"
 * Unmanaged mods (e.g. Game DLC) are prefixed with "*"
 * The mods are loaded in order.
 * Any mods listed earlier will overwrite files in mods listed later.
 *
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
 * This function returns an array of mod names in the order they appear in the file
 */
export function ParseModListText(modListContents: string): ModListItem[] {
    let modlist = new Array<ModListItem>();
    const modlistLines = modListContents.split('\n');
    for (let line of modlistLines) {
        if (line.charAt(0) === '#' || line === '') {
            continue;
        }
        let indic = line.charAt(0);
        let modName = line.substring(1);
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
    const modlistContents = readFileSync(modlistPath, 'utf8');
    if (!modlistContents) {
        return undefined;
    }
    return ParseModListText(modlistContents);
}

export async function WriteChangesToIni(gameIniPath: string, skyrimIni: INIData) {
    const file = openSync(gameIniPath, 'w');
    if (!file) {
        return false;
    }
    writeFileSync(file, ini.stringify(skyrimIni));
    return false;
}

// parse moshortcut URI
export function parseMoshortcutURI(moshortcutURI: string): { instanceName: string; exeName: string } {
    let moshortcutparts = moshortcutURI.replace('moshortcut://', '').split(':');
    let instanceName = moshortcutparts[0] || 'portable';
    let exeName = moshortcutparts[1];
    return { instanceName, exeName };
}

export function checkPDSModExistsAndEnabled(modlist: Array<ModListItem>) {
    return modlist.findIndex((mod) => mod.name === PDSModName && mod.enabled === ModEnabledState.enabled) !== -1;
}

export function checkAddressLibraryExistsAndEnabled(modlist: Array<ModListItem>, game: PapyrusGame) {
    if (game === PapyrusGame.skyrimSpecialEdition) {
        //TODO: check for the current version of skyrim SE'
        // Right now, we just ensure both versions are installed
        return (
            modlist.findIndex(
                (mod) => mod.name === AddressLibrarySKSEModName && mod.enabled === ModEnabledState.enabled
            ) !== -1 &&
            modlist.findIndex(
                (mod) => mod.name === AddressLibrarySKSEAEModName && mod.enabled === ModEnabledState.enabled
            ) !== -1
        );
    } else if (game === PapyrusGame.fallout4) {
        return (
            modlist.findIndex(
                (mod) => mod.name === AddressLibraryF4SEModName && mod.enabled === ModEnabledState.enabled
            ) !== -1
        );
    }
    return modlist.findIndex((mod) => mod.name === PDSModName && mod.enabled === ModEnabledState.enabled) !== -1;
}

export function AddModToBeginningOfModList(p_modlist: Array<ModListItem>, mod: ModListItem) {
  let modlist = p_modlist;
  // check if the mod is already in the modlist
  let modIndex = modlist.findIndex(
        (m) => m.name === mod.name
      );
  if (modIndex !== -1) {
      // if the mod is already in the modlist, remove it
      modlist = modlist.splice(modIndex, 1);
  }

  modlist = [mod].concat(modlist);
  return modlist;
}

export function AddRequiredModsToModList(p_modlist: Array<ModListItem>, game: PapyrusGame) {
  // add the debug adapter to the modlist
  let modlist = p_modlist;
  let debugAdapterMod = new ModListItem(PDSModName, ModEnabledState.enabled);

  let addressLibraryMods = getAddressLibNames(game).map(d => new ModListItem(d, ModEnabledState.enabled));

  // ensure address libs load before debug adapter by putting them after the debug adapter in the modlist
  modlist = AddModToBeginningOfModList(modlist, debugAdapterMod);
  modlist = addressLibraryMods.reduce((modlist, mod) => 
    AddModToBeginningOfModList(modlist, mod), modlist
  );
  return modlist;
}

export function ModListToText(modlist: Array<ModListItem>) {
    let modlistText = '# This file was automatically generated by Mod Organizer.';
    for (let mod of modlist) {
        modlistText += mod.enabled + mod.name + '\n';
    }
    return modlistText;
}

export function WriteChangesToModListFile(modlistPath: string, modlist: Array<ModListItem>) {
    let modlistContents = ModListToText(modlist);
    if (!openSync(modlistPath, 'w')) {
        return false;
    }
    writeFileSync(modlistPath, modlistContents);
    return true;
}
