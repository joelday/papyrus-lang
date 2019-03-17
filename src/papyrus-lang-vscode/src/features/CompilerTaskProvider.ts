import { TaskProvider, Task, tasks } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPapyrusTaskDefinition, TaskOf } from './CompilerTaskDefinition';
import { PapyrusGame } from '../PapyrusGame';
import { ILanguageClientManager } from '../server/LanguageClientManager';

export class CompilerTaskProvider implements TaskProvider, Disposable {
    private _taskProviderHandle: Disposable;
    private _configProvider: IExtensionConfigProvider;
    private _clientManager: ILanguageClientManager;

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @ILanguageClientManager clientManager: ILanguageClientManager
    ) {
        this._taskProviderHandle = tasks.registerTaskProvider('papyrus', this);

        this._configProvider = configProvider;
        this._clientManager = clientManager;
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        return [];
    }

    resolveTask(task: TaskOf<IPapyrusTaskDefinition>, token?: CancellationToken): Promise<Task> {
        throw new Error('Method not implemented.');
    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}
