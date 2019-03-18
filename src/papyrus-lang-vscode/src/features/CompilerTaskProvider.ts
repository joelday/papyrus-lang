// import { TaskProvider, Task, tasks, ProcessExecution, workspace, WorkspaceFolder, TaskScope } from 'vscode';
// import { CancellationToken, Disposable } from 'vscode-jsonrpc';
// import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
// import { IPapyrusTaskDefinition, TaskOf, IPapyrusTaskOptions } from './CompilerTaskDefinition';
// import { ILanguageClientManager } from '../server/LanguageClientManager';
// import { PapyrusGame } from '../PapyrusGame';
// import { ICreationKitInfo, ICreationKitInfoProvider } from '../CreationKitInfoProvider';
// import { getDefaultFlagsFileNameForGame } from '../Utilities';
// import { take } from 'rxjs/operators';
// import * as path from 'path';

// function taskOptionsToCommandLineArguments(options: IPapyrusTaskOptions, creationKitInfo: ICreationKitInfo) {
//     const args: string[] = [];
//     const isFallout4 = options.game === PapyrusGame.fallout4;
//     const project = isFallout4 && options.project;

//     if (project) {
//         args.push(project);
//     } else if (options.scripts) {
//         if (options.scripts.files) {
//             args.push(options.scripts.files.join(';'));
//         } else if (options.scripts.folders) {
//             args.push(options.scripts.folders.join(';'));
//             args.push('-all');

//             if (isFallout4 && options.scripts.noRecurse) {
//                 args.push('-norecurse');
//             }
//         }
//     }

//     if (isFallout4) {
//         if (options.final) {
//             args.push('-final');
//         }

//         if (options.release) {
//             args.push('-release');
//         }

//         args.push('-ignorecwd');
//     }

//     if (options.debugOutput) {
//         args.push('-debug');
//     }

//     if (options.optimize) {
//         args.push('-optimize');
//     }

//     const flags = !options.flags && !project ? getDefaultFlagsFileNameForGame(options.game) : options.flags;
//     if (flags) {
//         args.push(`-flags="${flags}"`);
//     }

//     const output = !options.output && !project ? creationKitInfo.config.Papyrus.sScriptCompiledFolder : options.output;
//     if (output) {
//         args.push(`-output="${output}"`);
//     }

//     return args;
// }

// export class CompilerTaskProvider implements TaskProvider, Disposable {
//     private _taskProviderHandle: Disposable;
//     private _creationKitInfoProvider: ICreationKitInfoProvider;

//     constructor(@ICreationKitInfoProvider creationKitInfoProvider: ICreationKitInfoProvider) {
//         this._creationKitInfoProvider = creationKitInfoProvider;
//         this._taskProviderHandle = tasks.registerTaskProvider('papyrus', this);
//     }

//     async provideTasks(token?: CancellationToken): Promise<Task[]> {
//         // const creationKitInfo = await this._creationKitInfoProvider.infos
//         //     .get(PapyrusGame.fallout4)
//         //     .pipe(take(1))
//         //     .toPromise();

//         // return workspace.workspaceFolders.map((folder) => {
//         //     const task = new Task(
//         //         { type: 'papyrus', papyrus: { game: PapyrusGame.fallout4, project: '' } },
//         //         folder,
//         //         'default',
//         //         'papyrus'
//         //     );

//         //     task.execution = new ProcessExecution(
//         //         path.join(creationKitInfo.resolvedCompilerPath, 'PapyrusCompiler.exe'),
//         //         taskOptionsToCommandLineArguments(task.definition.papyrus, creationKitInfo)
//         //     );

//         //     return task;
//         // });

//         return undefined;
//     }

//     async resolveTask(task: TaskOf<IPapyrusTaskDefinition>, token?: CancellationToken): Promise<Task> {
//         const creationKitInfo = await this._creationKitInfoProvider.infos
//             .get(task.definition.papyrus.game)
//             .pipe(take(1))
//             .toPromise();

//         if (token.isCancellationRequested) {
//             return null;
//         }

//         task.execution = new ProcessExecution(
//             path.join(creationKitInfo.resolvedCompilerPath, 'PapyrusCompiler.exe'),
//             taskOptionsToCommandLineArguments(task.definition.papyrus, creationKitInfo)
//         );

//         return task;
//     }

//     dispose() {
//         this._taskProviderHandle.dispose();
//     }
// }
