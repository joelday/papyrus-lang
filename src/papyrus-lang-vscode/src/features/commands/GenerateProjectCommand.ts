import { GameCommandBase } from './GameCommandBase';
import { PapyrusGame } from '../../PapyrusGame';

export class GenerateProjectCommand extends GameCommandBase {

    constructor(name: string) {
        super(name, [PapyrusGame.skyrimSpecialEdition]); // pass additional args for execute() and onExecute() here
    }

    protected async onExecute() {

    }
}