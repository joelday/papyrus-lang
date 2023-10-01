import { injectable } from 'inversify';
import { debug } from 'vscode';
import { IPapyrusDebugConfiguration } from '../../debugger/PapyrusDebugSession';
import { PapyrusGame, getShortDisplayNameForGame } from '../../PapyrusGame';
import { GameCommandBase } from './GameCommandBase';

@injectable()
export class AttachDebuggerCommand extends GameCommandBase {
    constructor() {
        super('attachDebugger', [PapyrusGame.fallout4, PapyrusGame.skyrimSpecialEdition]);
    }

    protected async onExecute(game: PapyrusGame) {
        debug.startDebugging(undefined, {
            game,
            name: getShortDisplayNameForGame(game),
            type: 'papyrus',
            request: 'attach',
        } as IPapyrusDebugConfiguration);
    }
}
