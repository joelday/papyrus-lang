import { TaskProvider, TaskDefinition, Task, tasks, ProcessExecution, workspace, WorkspaceFolder, TaskScope } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider, ExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPyroTaskDefinition, TaskOf, IPyroTaskOptions } from './PyroTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
import { getDefaultFlagsFileNameForGame } from '../Utilities';
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


    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider extensionConfigProvider: IExtensionConfigProvider,
        @IWorkspaceSetupService workspaceSetupService: IWorkspaceSetupService
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._extensionConfigProvider = extensionConfigProvider;
        this._taskProviderHandle = tasks.registerTaskProvider('pyro', this);
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        return workspace.workspaceFolders.map((folder) => {
            const task = new Task(
                {
                    type: 'pyro',
                    pyro: {
                        game: PapyrusGame.fallout4,
                        project: ''
                    }
                },
                folder,
                'default',
                'pyro'
            );

            // task.execution = new ProcessExecution( xxx );

            return task;
        });
    }

    async resolveTask(task: TaskOf<IPyroTaskDefinition>, token?: CancellationToken): Promise<Task> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(task.definition.papyrus.game)
            .pipe(take(1))
            .toPromise();

        if (token.isCancellationRequested) {
            return null;
        }

        // task.execution = new ProcessExecution(        );

        return task;
    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}
