import { existsSync, statSync } from 'fs';
import { PapyrusGame } from '../PapyrusGame';
import { MO2Config } from './PapyrusDebugSession';

import { injectable, interfaces } from 'inversify';
import path from 'path';
import { GetPapyrusGameFromMO2GameID, getGameINIFromMO2Profile } from './MO2Helpers';
import * as MO2Lib from '../common/MO2Lib';
import { LaunchCommand } from './DebugLauncherService';

export interface MO2ProfileData {
    name: string;
    folderPath: string;
    /**
     * Path to the ini file that contains the settings for this profile
     * Should always be present in profile folder and should always be named "settings.ini"
     * @type {string}
     */
    settingsIniPath: string;
    /**
     * Path to the txt file that contains the mod list for this profile
     * Should always be present in profile folder and should always be named "modlist.txt"
     * @type {string}
     */
    modListPath: string;
    /**
     * Path to the ini file that contains the Skyrim/Fallout 4/Starfield settings.
     * Depending if the profile has local settings, this is either present in the profile folder or in the global save game folder.
     * Should alays be named "Skyrim.ini", "Fallout4.ini", or "Starfield.ini"
     * @type {string}
     */
    gameIniPath: string;
}

export interface IMO2LaunchDescriptorFactory {
    createMO2LaunchDecriptor(
        launcherPath: string,
        launcherArgs: string[],
        mo2Config: MO2Config,
        game: PapyrusGame
    ): Promise<MO2LauncherDescriptor>;
}

@injectable()
export class MO2LaunchDescriptorFactory implements IMO2LaunchDescriptorFactory {
    constructor() {}
    // TODO: After testing, make these private
    public static async populateMO2ProfileData(name: string, profileFolder: string, game:PapyrusGame, gamePath: string): Promise<MO2ProfileData> {
        if (!existsSync(profileFolder)) {
            throw new Error(`Invalid MO2 profile: Could not find the profile folder ${profileFolder}}`);
        }
        // This is the path to the ini file that contains the settings for this profile
        // This should always be present; if it isn't, then something is wrong with the profile
        const settingsIniPath = path.join(profileFolder, 'settings.ini');
        if (!existsSync(settingsIniPath)) {
            throw new Error(`Invalid MO2 profile: Could not find the settings.ini file in ${profileFolder}}`);
        }
        const ModsListPath = path.join(profileFolder, 'modlist.txt');
        if (!existsSync(ModsListPath)) {
            throw new Error(`Invalid MO2 profile: Could not find the modlist.txt file`);
        }
        const ModsListData = await MO2Lib.ParseModListFile(ModsListPath);
        if (!ModsListData) {
            throw new Error(`Invalid MO2 profile: Mod list file is not parsable`);
        }
        const gameIniPath = await getGameINIFromMO2Profile(game, gamePath, profileFolder);
        return {
            name: name,
            folderPath: profileFolder,
            settingsIniPath: settingsIniPath,
            modListPath: ModsListPath,
            gameIniPath: gameIniPath,
        } as MO2ProfileData;
    }

    public static async PopulateMO2InstanceData(
        MO2EXEPath: string,
        instanceName: string,
        exeTitle: string,
        game: PapyrusGame,
        instanceINIPath?: string
    ) {
        let InstanceInfo: MO2Lib.MO2InstanceInfo | undefined;
        if (!instanceINIPath) {
            InstanceInfo = await MO2Lib.FindInstanceForEXE(MO2EXEPath, instanceName);
        } else {
            InstanceInfo = await MO2Lib.GetMO2InstanceInfo(instanceINIPath);
        }
        if (!InstanceInfo) {
            throw new Error(`Could not find the instance '${instanceName}' for the MO2 installation at ${MO2EXEPath}`);
        }
        const papgame = GetPapyrusGameFromMO2GameID(InstanceInfo.gameName);
        if (!papgame || papgame !== game) {
            throw new Error(`Instance ${instanceName} is not for game ${game}`);
        }
        if (InstanceInfo.gameDirPath === undefined) {
            throw new Error(`Instance ${instanceName} does not have a game directory path`);
        }
        if (!existsSync(InstanceInfo.profilesFolder) || !statSync(InstanceInfo.profilesFolder).isDirectory()) {
            throw new Error(`Could not find the profiles folder for instance ${instanceName}`);
        }
        if (!existsSync(InstanceInfo.modsFolder) || !statSync(InstanceInfo.modsFolder).isDirectory()) {
            throw new Error(`Could not find the mods folder for instance ${instanceName}`);
        }
        if (!InstanceInfo.customExecutables.filter((entry) => entry.title === exeTitle).length) {
            throw new Error(`Instance ${instanceName} does not have an executable named ${exeTitle}`);
        }
        return InstanceInfo;
    }

    public static async populateMO2LaunchConfiguration(
        launcherPath: string,
        launcherArgs: string[],
        mo2Config: MO2Config,
        game: PapyrusGame
    ): Promise<IMO2LauncherDescriptor> {
        // taken care of by debug config provider
        const { instanceName, exeName } = MO2Lib.parseMoshortcutURI(mo2Config.shortcutURI);
        if (!instanceName || !exeName) {
            throw new Error(`Could not parse the shortcut URI`);
        }

        const MO2EXEPath = launcherPath;
        if (!MO2EXEPath || !existsSync(MO2EXEPath) || !statSync(MO2EXEPath).isFile()) {
            throw new Error(`Could not find the Mod Organizer 2 executable path`);
        }
        let instanceData: MO2Lib.MO2InstanceInfo;
        try {
            instanceData = await this.PopulateMO2InstanceData(
                MO2EXEPath,
                instanceName,
                exeName,
                game,
                mo2Config.instanceIniPath
            );
        } catch (error) {
            throw new Error(`Could not populate the instance data: ${error}`);
        }
        const profile = mo2Config.profile || instanceData.selectedProfile;
        if (!profile) {
            throw new Error(`Could not find a profile to launch`);
        }
        // check if the instance is
        const profilePath = path.join(instanceData.profilesFolder, profile);
        // check if it exists and is directory
        if (!existsSync(profilePath) || !statSync(profilePath).isDirectory()) {
            throw new Error(`Could not find the profile '${profile}' in ${instanceData.profilesFolder}`);
        }
        const profileData: MO2ProfileData = await this.populateMO2ProfileData(profile, profilePath, game, instanceData.gameDirPath);
        const additionalArgs = launcherArgs;
        return {
            exeTitle: exeName,
            MO2EXEPath,
            additionalArgs,
            game,
            instanceInfo: instanceData,
            profileToLaunchData: profileData,
        } as IMO2LauncherDescriptor;
    }

    public async createMO2LaunchDecriptor(
        launcherPath: string,
        launcherArgs: string[],
        mo2Config: MO2Config,
        game: PapyrusGame
    ): Promise<MO2LauncherDescriptor> {
        if (!path.isAbsolute(launcherPath)) {
            throw new Error(`The launcher path must be an absolute path`);
        }
        let idescriptor: IMO2LauncherDescriptor;
        try {
            idescriptor = await MO2LaunchDescriptorFactory.populateMO2LaunchConfiguration(
                launcherPath,
                launcherArgs,
                mo2Config,
                game
            );
        } catch (error) {
            throw new Error(`Could not create the launch configuration: ${error}`);
        }

        return new MO2LauncherDescriptor(idescriptor);
    }
    dispose() {}
}

export interface IMO2LauncherDescriptor {
    exeTitle: string;
    MO2EXEPath: string;
    additionalArgs: string[];
    game: PapyrusGame;
    instanceInfo: MO2Lib.MO2InstanceInfo;
    profileToLaunchData: MO2ProfileData;
    getLaunchCommand(): LaunchCommand;
}

function joinArgs(args: string[]): string {
    const _args = args;
    for (const arg in args) {
        if (_args[arg].includes(' ') && !_args[arg].startsWith('"') && !_args[arg].endsWith('"')) {
            _args[arg] = `"${_args[arg]}"`;
        }
    }
    return _args.join(' ');
}
export class MO2LauncherDescriptor implements IMO2LauncherDescriptor {
    public readonly exeTitle: string = '';
    public readonly MO2EXEPath: string = '';
    public readonly additionalArgs: string[] = [];
    public readonly game: PapyrusGame = PapyrusGame.skyrim;
    public readonly instanceInfo: MO2Lib.MO2InstanceInfo = {} as MO2Lib.MO2InstanceInfo;
    public readonly profileToLaunchData: MO2ProfileData = {} as MO2ProfileData;

    constructor(idecriptor: IMO2LauncherDescriptor) {
        this.exeTitle = idecriptor.exeTitle;
        this.MO2EXEPath = idecriptor.MO2EXEPath;
        this.additionalArgs = idecriptor.additionalArgs;
        this.game = idecriptor.game;
        this.instanceInfo = idecriptor.instanceInfo;
        this.profileToLaunchData = idecriptor.profileToLaunchData;
    }

    public getLaunchCommand(): LaunchCommand {
        const command = this.MO2EXEPath;
        let cmdargs = ['-p', this.profileToLaunchData.name];
        if (this.instanceInfo.name !== 'portable') {
            cmdargs = cmdargs.concat(['-i', this.instanceInfo.name]);
        }
        cmdargs = cmdargs.concat('run', '-e', this.exeTitle);
        if (this.additionalArgs.length > 0) {
            cmdargs = cmdargs.concat(['-a', joinArgs(this.additionalArgs)]);
        }
        return {
            command: command,
            args: cmdargs,
        } as LaunchCommand;
    }
}

export const IMO2LaunchDescriptorFactory: interfaces.ServiceIdentifier<IMO2LaunchDescriptorFactory> =
    Symbol('mo2LaunchDescriptorFactory');
