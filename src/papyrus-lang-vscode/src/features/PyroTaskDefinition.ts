import { TaskDefinition, Task } from 'vscode';
import { PapyrusGame } from '../PapyrusGame';

export type TaskOf<T extends TaskDefinition> = { readonly definition: T } & Task;

export interface IPyroTaskDefinition extends TaskDefinition {
    readonly type: 'papyrus';
    readonly papyrus: IPyroTaskOptions;
}

export interface IPyroTaskOptions {
    readonly game: PapyrusGame;
    readonly ppj: string;
}
