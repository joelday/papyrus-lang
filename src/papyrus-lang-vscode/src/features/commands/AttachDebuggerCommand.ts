import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { IDebugSupportInstaller, DebugSupportInstallState } from '../../debugger/DebugSupportInstaller';
import { window, ProgressLocation, debug, workspace, Uri } from 'vscode';
import { IPapyrusDebugConfiguration } from '../../debugger/PapyrusDebugSession';

export class AttachDebuggerCommand extends CommandBase {
    constructor() {
        super('papyrus.fallout4.attachDebugger');
    }

    protected async onExecute() {
        debug.startDebugging(undefined, {
            game: 'fallout4',
            name: 'Fallout 4',
            type: 'papyrus',
            request: 'attach',
        } as IPapyrusDebugConfiguration);
    }
}
