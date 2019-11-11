import { window, Uri, ExtensionContext, workspace } from 'vscode';
import { take } from 'rxjs/operators';
import { IExtensionContext } from '../../common/vscode/IocDecorators';
import { IExtensionConfigProvider } from '../../ExtensionConfigProvider';
import { GameCommandBase } from './GameCommandBase';
import { PapyrusGame } from '../../PapyrusGame';
import { resolveInstallPath } from '../../Paths';
import { copyAndFillTemplate } from '../../Utilities';

// import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { promisify } from 'util';

const readFile = promisify(fs.readFile);
const writeFile = promisify(fs.writeFile);
const exists = promisify(fs.exists);
const mkdir = promisify(fs.mkdir);
const copyFile = promisify(fs.copyFile);
//const removeFile = promisify(fs.unlink);


export class GenerateProjectCommand extends GameCommandBase<[string]> {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;

    constructor(@IExtensionContext context: ExtensionContext,
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider) {
        super("generateProject"); // pass additional args for execute() and onExecute() here
        this._context = context;
        this._configProvider = configProvider;
    }

    protected async onExecute(game: PapyrusGame, ...args: [string]) {
        const config = (await this._configProvider.config.pipe(take(1)).toPromise())[game];

        const defaultProjectSubdir = {
            'fallout4': "Data",
            'skyrimSpecialEdition': "Data",
            'skyrim': "Data"
        };

        const resourceDir = {
            'fallout4': 'fo4',
            'skyrimSpecialEdition': "sse",
            'skyrim': "tesv"
        };

        // Ignore the context menu folder for Fallout 4 because we don't currenlty have a solution for anything other
        // than the game directory.
        if (game === PapyrusGame.fallout4) {
            args[0] = undefined;
        }
        let projectFolderUri: Uri = args[0] ? Uri.parse(args[0]) : Uri.file(path.join(config.installPath, defaultProjectSubdir[game]));

        console.log("Default projectFolderUri = " + projectFolderUri.fsPath);

        const dialogResult = window.showOpenDialog({
            canSelectFiles: false,
            canSelectFolders: true,
            canSelectMany: false,
            defaultUri: projectFolderUri,
            openLabel: "Select Folder"
        });

        const resultUriArray = await dialogResult.then(
            (uri) => {
                console.log("showOpenDialog fulfilled: " + uri);
                return uri;
            },
            (reason) => {
                console.log("showOpenDialog rejected: " + reason);
            });

        if (resultUriArray === undefined) {
            return;
        } else {
            projectFolderUri = resultUriArray[0];
        }

        console.log("Installing project files in: " + projectFolderUri.fsPath);
        const projectFolder = projectFolderUri.fsPath;

        const resourcePath = this._context.asAbsolutePath(path.join('resources', resourceDir[game]));

        const workspaceFilename = {
            'fallout4': "Fallout4.code-workspace",
            'skyrimSpecialEdition': "SkyrimSE.code-workspace",
            'skyrim': "SkyrimLE.code-workspace"
        }[game];

        const filesToCopy = [
            ['launch.json', '.vscode\\launch.json'],
            ['tasks.json', '.vscode\\tasks.json'],
            [workspaceFilename, workspaceFilename],
        ];
        let configGamePath = await resolveInstallPath(game, config.installPath, this._context);
        if (!configGamePath) {
            window.showErrorMessage(
                "Could not find game installation directory for " + PapyrusGame[game] +
                ". Is the game installed and configured in" +
                " the extension configuration?", "Ok");
            return;
        }
        const configSourcePath = path.join(configGamePath, "Data\\Source\\Scripts");
        if (game === PapyrusGame.fallout4) {
            filesToCopy.push(['fallout4.ppj', 'Scripts\\Source\\User\\fallout4.ppj']);
        } else if (game === PapyrusGame.skyrimSpecialEdition) {
            await copyAndFillTemplate(
                path.join(resourcePath, 'skyrimse.ppj'),
                path.join(projectFolder, 'skyrimse.ppj'),
                { 'SKYRIMSE_PATH': configSourcePath }
            );
        } else if (game === PapyrusGame.skyrim) {
            await copyAndFillTemplate(
                path.join(resourcePath, 'skyrimle.ppj'),
                path.join(projectFolder, 'skyrimle.ppj'),
                { 'SKYRIMLE_PATH': configSourcePath }
            );
        }

        let nerrs = 0;
        let already_exists: string[] = [];
        const errHandle = (e: any) => { if (e) { window.showWarningMessage(e.message); nerrs++; } };

        try {
            let dir = path.join(projectFolder, '.vscode');
            if (await exists(dir)) { already_exists.push(path.basename(dir)); }
            await mkdir(dir);
        } catch (e) {
            errHandle(e);
        }

        for (let job of filesToCopy) {
            try {
                let file = path.join(projectFolder, job[1]);
                if (await exists(file)) { already_exists.push(path.basename(file)); }
                await copyFile(path.join(resourcePath, job[0]), file, fs.constants.COPYFILE_EXCL);
            } catch (e) {
                errHandle(e);
            }
        }

        window.showInformationMessage("Project files installed to " + projectFolder
            + (nerrs ? ". (" + nerrs + " errors) " : "") +
            (already_exists.length ? "The following files alredy existed and were not replaced: "
                + already_exists.join(" ") : ""), "Ok");

    }

}