import { DebugSession, DebugConfiguration } from 'vscode';
import { PapyrusGame } from '../PapyrusGame';

export interface IPapyrusDebugSession extends DebugSession {
    readonly type: 'papyrus';
    readonly configuration: IPapyrusDebugConfiguration;
}

export interface MO2Config {
    /**
     * The shortcut URI for the Mod Organizer 2 profile to launch
     *
     * You can get this from the Mod Organizer 2 shortcut menu.
     *
     * It is in the format: `moshortcut://<instance_name>:<EXE_title>`.
     *
     * If the MO2 installation is portable, the instance name is blank.
     *
     *
     *
     * Examples:
     * - non-portable: `moshortcut://Skyrim Special Edition:SKSE`
     * - portable:     `moshortcut://:F4SE`
     */
    shortcutURI: string;
    /**
     * The name of the Mod Organizer 2 profile to launch with.
     *
     * Defaults to the currently selected profile
     */
    profile?: string;
    /**
     * The path to the Mod Organizer 2 instance ini for this game.
     * This is only necessary to be set if the debugger has difficulty finding the MO2 instance location
     *
     * - If the Mod Organizer 2 exe is a portable installation, this is located in the parent folder.
     * - If it is a non-portable installation, this in `%LOCALAPPDATA%/ModOrganizer/<game>/ModOrganizer.ini`
     *
     * Examples:
     * - `C:/Users/<YOUR_USER_NAME>/AppData/Local/ModOrganizer/Fallout4/ModOrganizer.ini`
     * - `C:/Modding/MO2/ModOrganizer.ini`
     */
    instanceIniPath?: string;
}

export interface XSEConfig {}

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

    //TODO: split these into separate interfaces
    /**
     * The type of launcher to use
     *
     * - 'XSE':  Launches the game using SKSE/F4SE without a mod manager
     * - 'MO2':  Launches the game using Mod Organizer 2
     * */
    launchType?: 'XSE' | 'MO2';

    /**
     * The path to the launcher executable
     *
     * - If the launch type is 'MO2', this is the path to the Mod Organizer 2 executable.
     * - If the launch type is 'XSE', this is the path to the f4se/skse loader executable.
     *
     * Examples:
     * - "C:/Program Files/Mod Organizer 2/ModOrganizer.exe"
     * - "C:/Program Files (x86)/Steam/steamapps/common/Skyrim Special Edition/skse64_loader.exe"
     * - "C:/Program Files (x86)/Steam/steamapps/common/Fallout 4/f4se_loader.exe"
     *
     */
    launcherPath?: string;

    /**
     *
     * Configuration for Mod Organizer 2
     *
     * Only used if launchType is 'MO2'
     *
     */
    mo2Config?: MO2Config;

    /**
     * (optional, advanced) Additional arguments to pass to the launcher
     */
    args?: string[];

    /**
     * Ignore debugger configuration checks and launch
     */
    ignoreConfigChecks?: boolean;
}
