import { inject, injectable } from 'inversify';
import { ProviderResult, DebugConfigurationProvider, CancellationToken, WorkspaceFolder, debug, Disposable, DebugConfiguration } from 'vscode';
import { IPathResolver } from '../common/PathResolver';
import { PapyrusGame } from "../PapyrusGame";
import { MO2Config, IPapyrusDebugConfiguration } from './PapyrusDebugSession';

// TODO: Auto install F4SE plugin
// TODO: Warn if port is not open/if Fallout4.exe is not running

// Possibly based on custom language server requests:
// TODO: Resolve project from whichever that includes the active editor file.
// TODO: Provide configurations based on .ppj files in current directory.


@injectable()
export class PapyrusDebugConfigurationProvider implements DebugConfigurationProvider, Disposable {
    private readonly _registration: Disposable;
    private readonly _pathResolver: IPathResolver;

    constructor(
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._pathResolver = pathResolver;
        this._registration = debug.registerDebugConfigurationProvider('papyrus', this);
    }

    async provideDebugConfigurations(
        _folder: WorkspaceFolder | undefined,
        _token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration[]> {
        let PapyrusAttach = {
            type: 'papyrus',
            name: 'Fallout 4',
            game: PapyrusGame.fallout4,
            request: 'attach',
            projectPath: '${workspaceFolder}/${1:Project.ppj}',
        } as IPapyrusDebugConfiguration;
        let PapyrusMO2Launch = {
            type: 'papyrus',
            name: 'Fallout 4 (Launch with MO2)',
            game: PapyrusGame.fallout4,
            request: 'launch',
            launchType: 'mo2',
            mo2Config: {
                MO2EXEPath: 'C:/Modding/MO2/ModOrganizer.exe',
                shortcut: 'moshortcut://Fallout 4:F4SE',
                modsFolder: '${env:LOCALAPPDATA}/ModOrganizer/Fallout 4/mods',
                args: ['-skipIntro']
            } as MO2Config
        } as IPapyrusDebugConfiguration;
        let PapyruseXSELaunch = {
            type: 'papyrus',
            name: 'Fallout 4 (Launch with F4SE)',
            game: PapyrusGame.fallout4,
            request: 'launch',
            launchType: 'XSE',
            XSELoaderPath: 'C:/Program Files (x86)/Steam/steamapps/common/Fallout 4/f4se_loader.exe',
            args: ['-skipIntro']
        } as IPapyrusDebugConfiguration;
        return [
            PapyrusAttach,
            PapyrusMO2Launch,
            PapyruseXSELaunch
        ];
    }

    async resolveDebugConfiguration(
        folder: WorkspaceFolder | undefined,
        debugConfiguration: IPapyrusDebugConfiguration,
        token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration | null | undefined> {
        if (debugConfiguration.game !== undefined && debugConfiguration.request !== undefined)
        {
            if (debugConfiguration.request === 'launch')
            {
                if (debugConfiguration.launchType === 'mo2')
                {
                    if (debugConfiguration.mo2Config !== undefined && debugConfiguration.mo2Config.modsFolder !== undefined && debugConfiguration.mo2Config.MO2EXEPath !== undefined)
                    {
                        return debugConfiguration;
                    }
                }
                else if (debugConfiguration.launchType === 'XSE')
                {
                    if (debugConfiguration.XSELoaderPath !== undefined)
                    {
                        return debugConfiguration;
                    }
                }
            }
            else if (debugConfiguration.request === 'attach')
            {
                return debugConfiguration;
            }
        }
        throw new Error("Invalid debug configuration.");
        return undefined;
    }
    
    async substituteEnvVars(string: string): Promise<string> {
        let appdata = process.env.LOCALAPPDATA;
        let username = process.env.USERNAME;
        if (appdata){
            string = string.replace('${env:LOCALAPPDATA}', appdata);
        }
        if (username){
            string = string.replace('${env:USERNAME}', username);
        }
        return string;
    }


    // TODO: Check that all of these exist
    async prepMo2Config(mo2Config: MO2Config, game: PapyrusGame): Promise<MO2Config> {
        let modFolder = mo2Config.modsFolder || await this._pathResolver.getModDirectoryPath(game);
        return {
            MO2EXEPath: await this.substituteEnvVars(mo2Config.MO2EXEPath),
            shortcut: mo2Config.shortcut,
            modsFolder: await this.substituteEnvVars(mo2Config.modsFolder || ""),
            profile: mo2Config.profile || "Default",
            profilesFolder: mo2Config.profilesFolder ? await this.substituteEnvVars(mo2Config?.profilesFolder) : undefined,
            args: mo2Config.args || []
        } as MO2Config;
    }

    async resolveDebugConfigurationWithSubstitutedVariables(
        folder: WorkspaceFolder | undefined,
        debugConfiguration: IPapyrusDebugConfiguration,
        token?: CancellationToken
    ): Promise<IPapyrusDebugConfiguration | null | undefined> {
        if (debugConfiguration.request === 'launch')
        {
            if (debugConfiguration.launchType === 'mo2')
            {
                if (debugConfiguration.mo2Config === undefined)
                {
                    return undefined;
                }
                debugConfiguration.mo2Config = await this.prepMo2Config(debugConfiguration.mo2Config, debugConfiguration.game);
                return debugConfiguration
            }

            else if (debugConfiguration.launchType === 'XSE')
            {
                if(debugConfiguration.XSELoaderPath === undefined)
                {
                    return undefined;
                }
                debugConfiguration.XSELoaderPath = await this.substituteEnvVars(debugConfiguration.XSELoaderPath);
                return debugConfiguration;
            }
        }
        // else...
        else if (debugConfiguration.request === 'attach')
        {
            return debugConfiguration;
        }
        throw new Error("Invalid debug configuration.");
        return undefined;
    }
 
    dispose() {
        this._registration.dispose();
    }
}
