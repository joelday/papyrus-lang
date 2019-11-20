import {
    TaskProvider, Task, tasks, ProcessExecution, workspace, TaskScope,
    RelativePattern, GlobPattern, FileSystemWatcher, Uri, window
} from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';

import { IPyroTaskDefinition, TaskOf, PyroGameToPapyrusGame } from './PyroTaskDefinition';
import { PapyrusGame, getWorkspaceGameFromProjects, getWorkspaceGame } from '../PapyrusGame';
import { IPathResolver, PathResolver } from '../common/PathResolver';


export class PyroTaskProvider implements TaskProvider, Disposable {
    private readonly _taskProviderHandle: Disposable;
    private readonly _pathResolver: IPathResolver;
    private _taskCachePromise: Promise<Task[]> | undefined = undefined;
    private readonly _projPattern: GlobPattern;
    private readonly _fileWatcher: FileSystemWatcher;
    private readonly _source: string = "pyro";

    constructor(
        @IPathResolver pathResolver: PathResolver
    ) {
        this._pathResolver = pathResolver;

        this._taskProviderHandle = tasks.registerTaskProvider('pyro', this);

        if (!workspace.workspaceFolders) {
            return;
        }

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

        // search for all .PPJ files in workspace
        const ppjFiles: Uri[] = await workspace.findFiles(this._projPattern, undefined, undefined, token);

        let tasks: Task[] = [];
        const game: PapyrusGame | undefined = await getWorkspaceGameFromProjects(ppjFiles);
        if (!game) {
            window.showWarningMessage(
                "Could not find a ppj file in this workspace with a game type specified."
                + "  Please specify a game type in your ppj file or use the Generate Project Files command for a"
                + " template.", "Ok");
            return tasks;
        }

        // provide a build task for each one found
        for (let uri of ppjFiles) {
            let taskDef: IPyroTaskDefinition = {
                type: this._source,
                projectFile: workspace.asRelativePath(uri),
                gamePath: await this._pathResolver.getInstallPath(game)
            };
            tasks.push(await this.createTaskForDefinition(taskDef));
        }

        return tasks;
    }

    async resolveTask(task: TaskOf<IPyroTaskDefinition>, token?: CancellationToken): Promise<Task | undefined> {
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
        if (!definition.gamePath) {
        }
        return this.createTaskForDefinition(definition);
    }

    private async createTaskForDefinition(taskDef: IPyroTaskDefinition) {
        let argv: string[] = [];

        // Required arguments
        argv.push('--input-path');
        argv.push(taskDef.projectFile);

        // Build arguments
        if (taskDef.logPath) {
            argv.push('--log-path');
            argv.push(taskDef.logPath);
        }
        if (taskDef.anonymize === false) {
            argv.push('--no-anonymize');
        }
        if (taskDef.archive === false) {
            argv.push('--no-bsarch');
        }
        if (taskDef.incremental === false) {
            argv.push('--no-incremental-build');
        }
        if (taskDef.parallelize === false) {
            argv.push('--no-parallel');
        }
        if (taskDef.workerLimit) {
            argv.push('--worker-limit');
            argv.push(taskDef.workerLimit.toString());
        }

        // Compiler arguments
        if (taskDef.compilerPath) {
            argv.push('--compiler-path');
            argv.push(taskDef.compilerPath);
        }
        if (taskDef.flagsPath) {
            argv.push('--flags-path');
            argv.push(taskDef.flagsPath);
        }
        if (taskDef.outputPath) {
            argv.push('--output-path');
            argv.push(taskDef.outputPath);
        }

        // Game arguments
        let game: PapyrusGame | undefined;
        if (taskDef.game) {
            argv.push('--game-type');
            argv.push(taskDef.game);
            game = PyroGameToPapyrusGame[game];
        } else {
            game = await getWorkspaceGame();
        }
        if (taskDef.gamePath) {
            argv.push('--game-path');
            argv.push(taskDef.gamePath);
        } else {
            argv.push('--game-path');
            argv.push(await this._pathResolver.getInstallPath(game));
        }
        if (taskDef.registryPath) {
            argv.push('--registry-path');
            argv.push(taskDef.registryPath);
        }

        // bsarch arguments
        if (taskDef.bsarchPath) {
            argv.push('--bsarch-path');
            argv.push(taskDef.bsarchPath);
        }
        if (taskDef.archivePath) {
            argv.push('--archive-path');
            argv.push(taskDef.archivePath);
        }
        if (taskDef.tempPath) {
            argv.push('--temp-path');
            argv.push(taskDef.tempPath);
        }

        const pyroAbsPath = await this._pathResolver.getPyroCliPath();
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