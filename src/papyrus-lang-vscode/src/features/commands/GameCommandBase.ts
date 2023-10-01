import { injectable, unmanaged } from 'inversify';
import { commands, Disposable } from 'vscode';
import { PapyrusGame, getGames } from "../../PapyrusGame";

@injectable()
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export abstract class GameCommandBase<TArgs extends any[] = void[], TResult = void> implements Disposable {
    private readonly _name: string;
    private readonly _registrations: Disposable[];

    constructor(@unmanaged() name: string, @unmanaged() supportedGames: PapyrusGame[] = getGames()) {
        this._name = name;

        this._registrations = supportedGames.map((game) =>
            commands.registerCommand(`papyrus.${game}.${this._name}`, (...args) => {
                return this.onExecute(game, ...((args || []) as TArgs));
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
