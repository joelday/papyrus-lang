import { TaskDefinition, Task } from 'vscode';

export type TaskOf<T extends TaskDefinition> = { readonly definition: T } & Task;


// TODO: Ask fire what these are going to be

export enum PyroGameToPapyrusGame {
    fo4 = 'fallout4',
    tesv = 'skyrim',
    sse = 'skyrimSpecialEdition',
}

export enum PyroGame {
    fallout4 = 'fo4',
    skyrim = 'tesv',
    skyrimSpecialEdition = 'sse',
}

export interface IPyroTaskDefinition extends TaskDefinition {
    // required arguments
    readonly projectFile: string;
    // build arguments
    readonly logPath?: string;
    readonly anonymize?: boolean;
    readonly archive?: boolean;
    readonly incremental?: boolean;
    readonly parallelize?: boolean;
    readonly workerLimit?: number;
    // compiler arguments
    readonly compilerPath?: string;
    readonly flagsPath?: string;
    readonly outputPath?: string;
    // game arguments
    readonly game?: PyroGame;
    readonly gamePath?: string;
    readonly registryPath?: string;
    // bsarch arguments
    readonly bsarchPath?: string;
    readonly archivePath?: string;
    readonly tempPath?: string;
}
