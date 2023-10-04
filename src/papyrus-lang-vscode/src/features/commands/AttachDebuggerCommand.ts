import { injectable } from 'inversify';
import { debug } from 'vscode';
import { IPapyrusDebugConfiguration } from '../../debugger/PapyrusDebugSession';
import { PapyrusGame, getShortDisplayNameForGame } from '../../PapyrusGame';
import { GameCommandBase } from './GameCommandBase';

@injectable()
export class AttachDebuggerCommand extends GameCommandBase {
    constructor() {
        super('attachDebugger', [PapyrusGame.fallout4, PapyrusGame.skyrimSpecialEdition, PapyrusGame.starfield]);
    }

    protected async onExecute(game: PapyrusGame) {
        // get the current workspace folder
        debug.startDebugging(debug.activeDebugSession?.workspaceFolder, {
            game,
            name: getShortDisplayNameForGame(game),
            type: 'papyrus',
            request: 'attach',
        } as IPapyrusDebugConfiguration);
    }
}
