import { DebugSession, DebugConfiguration } from 'vscode';
import { PapyrusGame } from '../PapyrusGame';

export interface IPapyrusDebugSession extends DebugSession {
    readonly type: 'papyrus';
    readonly configuration: IPapyrusDebugConfiguration;
}

export interface IPapyrusDebugConfiguration extends DebugConfiguration {
    readonly game: PapyrusGame;
    readonly projectPath?: string;
    readonly port?: number;
}
