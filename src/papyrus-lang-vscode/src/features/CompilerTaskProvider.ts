import { TaskProvider, Task, tasks } from 'vscode';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { IPapyrusTaskDefinition, TaskOf, IPapyrusTaskOptions } from './CompilerTaskDefinition';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { PapyrusGame } from '../PapyrusGame';
import { ICreationKitInfo } from '../CreationKitInfoProvider';

// function taskOptionsToCommandLineArguments(options: IPapyrusTaskOptions, creationKitInfo: ICreationKitInfo) {
//     const args: string[] = [];
//     const isFallout4 = options.game === PapyrusGame.fallout4;

//     if (isFallout4 && options.project) {
//         args.push(options.project);
//     } else {
//         const imports =

//         if (options.scripts.file) {
//             args.push(options.scripts.file);
//         } else if (options.scripts.folder) {
//             args.push(options.scripts.folder);
//             args.push('-all');

//             if (isFallout4 && options.scripts.noRecurse) {
//                 args.push('-norecurse');
//             }
//         }
//     }

//     if (options.debugOutput) {
//         args.push('-debug');
//     }

//     if (options.final) {
//         args.push('-final');
//     }

//     args.push('-final');

//     return args;
// }

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
