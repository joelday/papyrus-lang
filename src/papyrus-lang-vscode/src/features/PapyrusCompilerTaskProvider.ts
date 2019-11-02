import { TaskProvider, TaskDefinition, Task, tasks, ProcessExecution, workspace, WorkspaceFolder, TaskScope } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPapyrusCompilerTaskDefinition, TaskOf, IPapyrusCompilerTaskOptions } from './PapyrusCompilerTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getDefaultFlagsFileNameForGame } from '../Utilities';
import { take } from 'rxjs/operators';
import * as path from 'path';
import { IWorkspaceSetupService, WorkspaceSetupServiceState } from './WorkspaceSetupService';


// IMPLEMENT THIS
function taskOptionsToCommandLineArguments(opts: IPapyrusCompilerTaskOptions, creationKitInfo: ICreationKitInfo) {
    const args: string[] = [];
    const isFallout4 = opts.game === PapyrusGame.fallout4;
    if (opts.sources) {

    }

    return args;
}

export class PapyrusCompilerTaskProvider implements TaskProvider, Disposable {
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
        this._taskProviderHandle = tasks.registerTaskProvider('PapyrusCompiler', this);
        this._workspaceSetupService = workspaceSetupService;
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        while (true) {

            var setupState: WorkspaceSetupServiceState = await this._workspaceSetupService.getSetupState();

            switch (setupState) {
                case WorkspaceSetupServiceState.notSetup:
                    continue;
                // If sure this is not a papyrus project and isn't supposed to be then we don't install the task 
                case WorkspaceSetupServiceState.notPapyrus:
                    return undefined;
                    break;

                case WorkspaceSetupServiceState.skyrimNotSetup:
                    // return workspace.workspaceFolders.map((folder) => {
                    //     const task = new Task(
                    //         {
                    //             type: 'PapyrusCompiler',
                    //             pyro: {
                    //                 game: PapyrusGame.fallout4,
                    //                 sources: '${currentFile}'
                    //             }
                    //         },
                    //         folder,
                    //         'default',
                    //         'pyro'
                    //     );
                    return undefined;

                case WorkspaceSetupServiceState.skyrimSpecialEditionNotSetup:
                    // Setup SSE task
                    return undefined;

                case WorkspaceSetupServiceState.fallout4NotSetup:
                    // Setup FO4 task
                    return undefined;

                case WorkspaceSetupServiceState.skyrimSetup:
                case WorkspaceSetupServiceState.skyrimSpecialEditionSetup:
                case WorkspaceSetupServiceState.fallout4Setup:
                    break;

            }
            return undefined;
        }

        // think something like this needs to be done when state is setup
        // task.execution = new ProcessExecution( xxx );
        // return task;

    }

    async resolveTask(task: TaskOf<IPapyrusCompilerTaskDefinition>, token?: CancellationToken): Promise<Task> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(task.definition.papyrus.game)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }

        // task.execution = new ProcessExecution(    );

        return task;

    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}