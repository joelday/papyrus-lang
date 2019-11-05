import { TaskProvider, TaskDefinition, Task, tasks, ProcessExecution, workspace, WorkspaceFolder, TaskScope } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPapyrusCompilerTaskDefinition, TaskOf, IPapyrusCompilerTaskOptions } from './PapyrusCompilerTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getDefaultFlagsFileNameForGame } from '../Paths';
import { take } from 'rxjs/operators';
import * as path from 'path';
import { IWorkspaceSetupService, WorkspaceSetupServiceState } from './WorkspaceSetupService';


export class PapyrusCompilerTaskProvider implements TaskProvider, Disposable {
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _extensionConfigProvider: IExtensionConfigProvider;
    private readonly _workspaceSetupService: IWorkspaceSetupService;
    private readonly _taskProviderHandle: Disposable;

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider extensionConfigProvider: IExtensionConfigProvider,
        @IWorkspaceSetupService workspaceSetupService: IWorkspaceSetupService
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._extensionConfigProvider = extensionConfigProvider;
        this._workspaceSetupService = workspaceSetupService;
        this._taskProviderHandle = tasks.registerTaskProvider('PapyrusCompiler', this);

    }

    // This is for providing tasks associated with makefiles, essentially that are not defined in tasks.json
    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        this._workspaceSetupService.run();
        console.log("*** Attempting to run WorkspaceSetupService from PapyrusCompilerTaskProvider::provideTasks");
        // We don't actually want to do anything for this one. We could maybe provide a task per source folder maybe?
        return undefined;
    }

    async resolveTask(task: TaskOf<IPapyrusCompilerTaskDefinition>, token?: CancellationToken): Promise<Task> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(task.definition.papyrus.game)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }

        // Compile individual file or folder of files when this task is invoked. Without any automatic task providing
        // it will need to be defined in tasks.jason.
        // task.execution = new ProcessExecution(    );

        return task;

    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}