import * as vs from 'vscode';

export abstract class CommandBase<TArgs = undefined, TResult = undefined> implements vs.Disposable {
    private readonly _name: string;
    private readonly _registration: vs.Disposable;

    constructor(name: string) {
        this._name = name;
        this._registration = vs.commands.registerCommand(this._name, (args) => {
            return this.onExecute(args);
        });
    }

    execute(args: TArgs) {
        return vs.commands.executeCommand<TResult>(this._name, args);
    }

    dispose() {
        this._registration.dispose();
    }

    protected abstract onExecute(args: TArgs): TResult | Thenable<TResult>;
}