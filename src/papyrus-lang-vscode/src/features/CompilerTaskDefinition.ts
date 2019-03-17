import { TaskDefinition, Task } from 'vscode';
import { PapyrusGame } from '../PapyrusGame';

export type TaskOf<T extends TaskDefinition> = { readonly definition: T } & Task;

export interface IPapyrusTaskDefinition extends TaskDefinition {
    readonly type: 'papyrus';
    readonly papyrus: IPapyrusTaskOptions;
}

export interface IPapyrusTaskOptions {
    readonly game: PapyrusGame;
    readonly imports?: string[];
    readonly flags?: string;
    readonly optimize?: boolean;
    readonly emitCompiledScripts?: boolean;
    readonly emitAssemblyFiles?: boolean;
    readonly debugOutput?: boolean;
    readonly output?: string;
    readonly final?: boolean;
    readonly release?: boolean;
    readonly project?: string;
    readonly scripts?: {
        readonly file?: string;
        readonly folder?: string;
        readonly noRecurse?: boolean;
    };
}
