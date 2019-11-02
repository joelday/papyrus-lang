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
    private readonly _creationKitInfoProvider: ICreationKitInfoProvider;
    private readonly _extensionConfigProvider: IExtensionConfigProvider;
    private readonly _workspaceSetupService: IWorkspaceSetupService;
    private _taskProviderHandle: Disposable;

    constructor(
        @ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider,
        @IExtensionConfigProvider extensionConfigProvider: IExtensionConfigProvider,
        @IWorkspaceSetupService workspaceSetupService: IWorkspaceSetupService
    ) {
        this._creationKitInfoProvider = creationKitInfoProvider;
        this._extensionConfigProvider = extensionConfigProvider;
        this._workspaceSetupService = workspaceSetupService;
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        const creationKitInfo = await this._creationKitInfoProvider.infos
            .get(PapyrusGame.fallout4)
            .pipe(take(1))
            .toPromise();

        var register: Promise<boolean> = new Promise<boolean>((resolve) => {
            var observable = this._workspaceSetupService.getObservable();

            observable.subscribe({
                next(setupState: WorkspaceSetupServiceState) {
                    switch (setupState) {

                        case WorkspaceSetupServiceState.notSetup:
                            return;

                        // If sure this is not a papyrus project and isn't supposed to be then we don't install the task 
                        case WorkspaceSetupServiceState.notPapyrus:
                            resolve(false);
                            return;

                        case WorkspaceSetupServiceState.skyrimNotSetup:
                            return; // actuallly return task

                        case WorkspaceSetupServiceState.skyrimSpecialEditionNotSetup:
                            // Setup SSE task
                            return;

                        case WorkspaceSetupServiceState.fallout4NotSetup:
                            // Setup FO4 task
                            return;

                        case WorkspaceSetupServiceState.skyrimSetup:
                        case WorkspaceSetupServiceState.skyrimSpecialEditionSetup:
                        case WorkspaceSetupServiceState.fallout4Setup:
                            return;

                        case WorkspaceSetupServiceState.isPapyrus:
                            resolve(true);
                            return;

                        case WorkspaceSetupServiceState.done:
                            observable.subscribe(); // unsubscribe
                            return;
                    }
                },
                error(err) {
                    console.error('PapyrusCompilerTaskProvider subscription: ' + err);
                },
                complete() {
                    console.log('PapyrusCompilerTaskProvider subscription: done');
                }
            });

        });

        if (await register.then(value => { return value; })) {
            this._taskProviderHandle = tasks.registerTaskProvider('PapyrusCompiler', this);
        }
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

        // task.execution = new ProcessExecution(    );

        return task;

    }

    dispose() {
        if (this._taskProviderHandle) {
            this._taskProviderHandle.dispose();
        }
    }
}