import { Disposable } from 'vscode';
import { GameCommandBase } from './GameCommandBase';
import { PapyrusGame } from '../../PapyrusGame';

export class GenerateProjectCommand extends GameCommandBase<[string]> {

    constructor() {
        super("generateProject"); // pass additional args for execute() and onExecute() here
    }

    protected async onExecute(game: PapyrusGame, ...args: [string]) {
        console.log("GenerateProjectCommand: game=" + game + "\narg0=" + args.join('\nargX='));
    }

    //    public async execute(game: PapyrusGame, ...args:[any]) {
    //    }
}