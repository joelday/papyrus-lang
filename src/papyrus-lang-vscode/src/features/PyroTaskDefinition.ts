import { TaskDefinition, Task } from 'vscode';
// import { PapyrusGame } from '../PapyrusGame';

export type TaskOf<T extends TaskDefinition> = { readonly definition: T } & Task;

export interface IPyroTaskDefinition extends TaskDefinition {
    readonly ppj: string;
    readonly game?: string;
    readonly ini?: string;
    readonly anonymize?: boolean;
    readonly index?: boolean;
    readonly parallelize?: boolean;
    readonly archive?: boolean;
}
