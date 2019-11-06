import * as path from 'path';
import { take } from 'rxjs/operators';

import {
    TaskProvider, Task, tasks, ProcessExecution, ShellExecution, workspace, WorkspaceFolder, TaskScope,
    RelativePattern, GlobPattern, FileSystemWatcher, Uri, ExtensionContext
} from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';

import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPyroTaskDefinition, TaskOf } from './PyroTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getDefaultFlagsFileNameForGame, getPyroCliPath } from '../Paths';
import { getWorkspaceGame } from '../Utilities';
import { IWorkspaceSetupService } from './WorkspaceSetupService';
import { RSA_PKCS1_OAEP_PADDING } from 'constants';
import { IExtensionContext } from '../common/vscode/IocDecorators';


export class PyroTaskProvider implements TaskProvider, Disposable {
    private readonly _taskProviderHandle: Disposable;
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _context: ExtensionContext;
    private readonly _extensionConfigProvider: IExtensionConfigProvider;
    private readonly _workspaceSetupService: IWorkspaceSetupService;
    private _ppjPromise: Thenable<Task[]> | undefined = undefined;
    private readonly _ppjPattern: GlobPattern;
    private readonly _fileWatcher: FileSystemWatcher;
    private readonly _source: string = "pyro";

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider extensionConfigProvider: IExtensionConfigProvider,
        @IWorkspaceSetupService workspaceSetupService: IWorkspaceSetupService,
        @IExtensionContext context: ExtensionContext
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._extensionConfigProvider = extensionConfigProvider;
        this._workspaceSetupService = workspaceSetupService;
        this._context = context;

        this._taskProviderHandle = tasks.registerTaskProvider('pyro', this);

        this._ppjPattern = new RelativePattern(workspace.workspaceFolders[0], "**/*.ppj");
        const fsw = this._fileWatcher = workspace.createFileSystemWatcher(this._ppjPattern);
        fsw.onDidChange(() => this._ppjPromise = undefined);
        fsw.onDidCreate(() => this._ppjPromise = undefined);
        fsw.onDidDelete(() => this._ppjPromise = undefined);

    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        if (token.isCancellationRequested) {
            return null;
        }
        if (!this._ppjPromise) {
            this._ppjPromise = this.getPyroTasks(token);
        }
        return this._ppjPromise;
    }

    async getPyroTasks(token?: CancellationToken): Promise<Task[]> {
        if (token.isCancellationRequested) {
            return null;
        }
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        // search for all .PPJ files in workspace
        const ppjFiles: Uri[] = await workspace.findFiles(this._ppjPattern, undefined, undefined, token);


        let tasks: Task[] = [];
        const game: PapyrusGame | undefined = await getWorkspaceGame();
        if (!game) {
            return tasks;
        }

        const pyroGame = papyrusGameToPyroGame(game);

        // provide a build task for each one found
        for (let uri of ppjFiles) {
            let ppj = workspace.asRelativePath(uri);
            let taskDef: IPyroTaskDefinition = {
                type: this._source,
                game: pyroGame,
                ppj: ppj
            };
            tasks.push(await this.createTaskForDefinition(taskDef));
        }

        return tasks;
    }

    async resolveTask(task: TaskOf<IPyroTaskDefinition>, token?: CancellationToken): Promise<Task | undefined> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(task.definition.papyrus.game)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }
        let definition = task.definition;
        if (definition === undefined) {
            definition = {
                type: this._source,
                ppj: "unknown.ppj"
            };
        }
        return this.createTaskForDefinition(definition);
    }

    private async createTaskForDefinition(taskDef: IPyroTaskDefinition) {
        let argv: string[] = [];
        argv.push('-i');
        argv.push(taskDef.ppj);
        if (taskDef.game) {
            argv.push('-g');
            argv.push(taskDef.game);
        }
        if (taskDef.ini) {
            argv.push('-c');
            argv.push(taskDef.ini);
        }
        if (taskDef.anonymize === false) {
            argv.push('--disable-anonymizer');
        }
        if (taskDef.index === false) {
            argv.push('--disable-indexer');
        }
        if (taskDef.parallelize === false) {
            argv.push('--disable-parallel');
        }
        if (taskDef.archive === false) {
            argv.push('--disable-bsarch');
        }

        const pyroAbsPath = this._context.asAbsolutePath(getPyroCliPath());
        console.log("New task, process: " + pyroAbsPath + " " + argv);
        const label = `Compile Project (${taskDef.ppj})`;
        taskDef['label'] = label;
        return new Task(
            taskDef,
            TaskScope.Workspace,
            label,
            taskDef.type,
            new ProcessExecution(pyroAbsPath, argv),
            ["$PapyrusCompiler"]
        );
    }

    dispose() {
        this._taskProviderHandle.dispose();
        this._fileWatcher.dispose();
    }
}


function papyrusGameToPyroGame(game: PapyrusGame): string {
    const pyroGames = new Map([
        [PapyrusGame.fallout4, 'fo4'],
        [PapyrusGame.skyrimSpecialEdition, 'sse'],
        [PapyrusGame.skyrim, 'tesv']
    ]);
    return pyroGames.get(game);
}