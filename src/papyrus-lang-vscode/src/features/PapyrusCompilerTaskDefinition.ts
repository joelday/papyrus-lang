import { TaskDefinition, Task } from 'vscode';
import { PapyrusGame } from '../PapyrusGame';

export type TaskOf<T extends TaskDefinition> = { readonly definition: T } & Task;

export interface IPapyrusCompilerTaskDefinition extends TaskDefinition {
    readonly type: 'papyrus';
    readonly papyrus: IPapyrusCompilerTaskOptions;
}

export interface IPapyrusCompilerTaskOptions {
    readonly game: PapyrusGame;
    readonly imports?: string[];
    readonly sources: string[];
    readonly output?: string;
}
