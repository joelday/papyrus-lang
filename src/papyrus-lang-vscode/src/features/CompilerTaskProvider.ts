import { TaskProvider, Task, tasks } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';

export class CompilerTaskProvider implements TaskProvider, Disposable {
    private _taskProviderHandle: Disposable;

    constructor(@IExtensionConfigProvider configProvider: IExtensionConfigProvider) {
        this._taskProviderHandle = tasks.registerTaskProvider('papyrus', this);
    }

    async provideTasks(token?: CancellationToken): Promise<Task[]> {
        return [];
    }

    resolveTask(task: Task, token?: CancellationToken): Promise<Task> {
        throw new Error('Method not implemented.');
    }

    dispose() {
        this._taskProviderHandle.dispose();
    }
}
