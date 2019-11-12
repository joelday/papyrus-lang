import { take } from 'rxjs/operators';

import {
    TaskProvider, Task, tasks, ProcessExecution, workspace, TaskScope,
    RelativePattern, GlobPattern, FileSystemWatcher, Uri, ExtensionContext
} from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';

import { IPyroTaskDefinition, TaskOf } from './PyroTaskDefinition';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getPyroCliPath } from '../Paths';
import { getWorkspaceGame } from '../Utilities';
import { IExtensionContext } from '../common/vscode/IocDecorators';


export class PyroTaskProvider implements TaskProvider, Disposable {
    private readonly _taskProviderHandle: Disposable;
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _context: ExtensionContext;
    private _taskCachePromise: Promise<Task[]> | undefined = undefined;
    private readonly _projPattern: GlobPattern;
    private readonly _fileWatcher: FileSystemWatcher;
    private readonly _source: string = "pyro";

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._context = context;

        this._taskProviderHandle = tasks.registerTaskProvider('pyro', this);

        this._projPattern = new RelativePattern(workspace.workspaceFolders[0], "**/*.ppj");
        const fsw = this._fileWatcher = workspace.createFileSystemWatcher(this._projPattern);
        fsw.onDidChange(() => this._taskCachePromise = undefined);
        fsw.onDidCreate(() => this._taskCachePromise = undefined);
        fsw.onDidDelete(() => this._taskCachePromise = undefined);

    }

    public provideTasks(token?: CancellationToken): Promise<Task[]> {
        if (token.isCancellationRequested) {
            return null;
        }
        if (!this._taskCachePromise) {
            this._taskCachePromise = this.getPyroTasks(token);
        }
        return this._taskCachePromise;
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
        const ppjFiles: Uri[] = await workspace.findFiles(this._projPattern, undefined, undefined, token);


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
                projectFile: ppj
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
                projectFile: "unknown.ppj"
            };
        }
        return this.createTaskForDefinition(definition);
    }

    private async createTaskForDefinition(taskDef: IPyroTaskDefinition) {
        let argv: string[] = [];
        argv.push('-i');
        argv.push(taskDef.projectFile);
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
        const label = `Compile Project (${taskDef.projectFile})`;
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