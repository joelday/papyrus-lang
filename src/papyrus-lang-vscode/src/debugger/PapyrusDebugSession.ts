import { DebugSession, DebugConfiguration } from 'vscode';
import { PapyrusGame } from "../PapyrusGame";

export interface IPapyrusDebugSession extends DebugSession {
    readonly type: 'papyrus';
    readonly configuration: IPapyrusDebugConfiguration;
}

export interface MO2Config {
    /**
     * The path to the Mod Organizer 2 executable
     * 
     * Example:
     * - "C:/Program Files/Mod Organizer 2/ModOrganizer.exe"
     */
    MO2EXEPath: string;
    /**
     * The shortcut URI for the Mod Organizer 2 profile to launch
     * 
     * You can get this from the Mod Organizer 2 shortcut menu
     * 
     * Example:
     * - "moshortcut://Skyrim Special Edition:SKSE"
    */
    shortcut: string;
    /**
     * The path to the Mod Organizer 2 mods folder
     * If not specified, defaults to the globally configured mods folder.
     * 
     * Example:
     * - "C:/Users/${USERNAME}/AppData/Local/ModOrganizer/Fallout 4/mods"
     */
    modsFolder?: string;
    /**
     * The name of the Mod Organizer 2 profile to launch with
     * Defaults to "Default"
     */
    profile?: string;
    /**
     * The path to the "profiles" folder for the Mod Organizer 2 instance.
     * 
     * If you have specified a custom mods folder in your MO2 instance configuration,
     * you must specify the profiles folder here.
     * 
     * If not specified, defaults to the "profiles" folder in the same parent directory as the mods folder.
     * 
     * Example:
     * - "C:/Users/${USERNAME}/AppData/Local/ModOrganizer/Fallout 4/profiles"
     */
    profilesFolder?: string;

    /**
     * Additional arguments to pass to Mod Organizer 2
     */
    args?: string[];
}

export interface IPapyrusDebugConfiguration extends DebugConfiguration {
    /** 
     * The game to debug ('fallout4', 'skyrim', 'skyrimSpecialEdition')
     */ 
    game: PapyrusGame;
    /**
     * The path to the project to debug
     */
    projectPath?: string;
    port?: number;
    /**
     * The type of debug request
     * - 'attach': Attaches to a running game
     * - 'launch': Launches the game
     */
    request: 'attach' | 'launch';
    /** 
     * The type of launch to use
     * 
     * - 'XSE': Launches the game using SKSE/F4SE without a mod manager
     * - 'mo2':  Launches the game using Mod Organizer 2
     * */
    launchType?: 'XSE' | 'mo2';
    /**
     * 
     * Configuration for Mod Organizer 2
     * 
     * Only used if launchType is 'mo2'
     * 
     */
    mo2Config?: MO2Config;

    /** 
     * The path to the f4se/skse loader executable
     * 
     * Examples: 
     * - "C:/Program Files (x86)/Steam/steamapps/common/Skyrim Special Edition/skse64_loader.exe"
     * - "C:/Program Files (x86)/Steam/steamapps/common/Fallout 4/f4se_loader.exe"
     */
    XSELoaderPath?: string;

    /**
     * Additional arguments to pass 
     * */
    args?: string[];

}
