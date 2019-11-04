import { TaskProvider, TaskDefinition, Task, tasks, ProcessExecution, workspace, WorkspaceFolder, TaskScope } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPyroTaskDefinition, TaskOf, IPyroTaskOptions } from './PyroTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getDefaultFlagsFileNameForGame } from '../Paths';
import { take } from 'rxjs/operators';
import * as path from 'path';
import { IWorkspaceSetupService } from './WorkspaceSetupService';


// IMPLEMENT THIS
function taskOptionsToCommandLineArguments(options: IPyroTaskDefinition, creationKitInfo: ICreationKitInfo) {
    const args: string[] = [];
    const isFallout4 = options.game === PapyrusGame.fallout4;

    if (options.ppj) {
        args.push(options.ppj);
    }

    return args;
}

export class PyroTaskProvider implements TaskProvider, Disposable {
    private readonly _taskProviderHandle: Disposable;
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _extensionConfigProvider: IExtensionConfigProvider;
    private readonly _workspaceSetupService: IWorkspaceSetupService;


    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider extensionConfigProvider: IExtensionConfigProvider,
        @IWorkspaceSetupService workspaceSetupService: IWorkspaceSetupService
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._extensionConfigProvider = extensionConfigProvider;
        this._workspaceSetupService = workspaceSetupService;
        // should this next line go in the constructor????
        this._taskProviderHandle = tasks.registerTaskProvider('pyro', this);
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }

        console.log("*** Attempting to run WorkspaceSetupService from PapyrusCompilerTaskProvider::provideTasks");
        this._workspaceSetupService.run();

        // search for all .PPJ files in workspace
        // provide a build task for each one found


        return undefined;
    }

    async resolveTask(task: TaskOf<IPyroTaskDefinition>, token?: CancellationToken): Promise<Task> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(task.definition.papyrus.game)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }

        // Execute Pyro on the task based on the tasks settings we get.
        // task.execution = new ProcessExecution(        );

        return task;
    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}
