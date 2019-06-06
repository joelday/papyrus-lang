import { commands, Disposable } from 'vscode';
import { PapyrusGame, getGames } from '../../PapyrusGame';

export abstract class GameCommandBase<TArgs extends any[] = void[], TResult = void> implements Disposable {
    private readonly _name: string;
    private readonly _registrations: Disposable[];

    constructor(name: string, supportedGames: PapyrusGame[] = getGames()) {
        this._name = name;

        this._registrations = supportedGames.map((game) =>
            commands.registerCommand(`papyrus.${game}.${this._name}`, (args) => {
                return this.onExecute(game, ...(args || []));
            })
        );
    }

    get name() {
        return this._name;
    }

    execute(game: PapyrusGame, ...args: TArgs) {
        return commands.executeCommand<TResult>(`papyrus.${game}.${this._name}`, ...args);
    }

    dispose() {
        for (const registration of this._registrations) {
            registration.dispose();
        }
    }

    protected abstract onExecute(game: PapyrusGame, ...args: TArgs): TResult | Thenable<TResult>;
}
