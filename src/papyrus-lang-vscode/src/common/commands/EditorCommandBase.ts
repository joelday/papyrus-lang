import * as vs from 'vscode';

export abstract class EditorCommandBase<TArgs = undefined, TResult = void> implements vs.Disposable {
    private readonly _name: string;
    private readonly _registration: vs.Disposable;

    constructor(name: string) {
        this._name = name;
        this._registration = vs.commands.registerTextEditorCommand(this._name, (editor, edit, args) => {
            return this.onExecute(editor, edit, args);
        });
    }

    execute(args: TArgs) {
        return vs.commands.executeCommand<TResult>(this._name, args);
    }

    dispose() {
        this._registration.dispose();
    }

    protected abstract onExecute(
        editor: vs.TextEditor, edit: vs.TextEditorEdit, args: TArgs): TResult | Thenable<TResult>;
}