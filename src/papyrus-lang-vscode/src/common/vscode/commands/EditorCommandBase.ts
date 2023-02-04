import { injectable, unmanaged } from 'inversify';
import { Disposable, commands, TextEditor, TextEditorEdit } from 'vscode';

@injectable()
export abstract class EditorCommandBase<TArgs extends any[] = void[], TResult = void> implements Disposable {
    private readonly _name: string;
    private readonly _registration: Disposable;

    constructor(@unmanaged() name: string) {
        this._name = name;
        this._registration = commands.registerTextEditorCommand(this._name, (editor, edit, ...args) => {
            return this.onExecute(editor, edit, ...((args || []) as TArgs));
        });
    }

    get name() {
        return this._name;
    }

    execute(...args: TArgs) {
        return commands.executeCommand<TResult>(this._name, ...args);
    }

    dispose() {
        this._registration.dispose();
    }

    protected abstract onExecute(editor: TextEditor, edit: TextEditorEdit, ...args: TArgs): TResult | Thenable<TResult>;
}
