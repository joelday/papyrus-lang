import * as vs from 'vscode';

export abstract class EditorAttachmentBase implements vs.Disposable {
    private readonly _editor: vs.TextEditor;
    private readonly _disposables: vs.Disposable[] = [];
    private _cachedIsVisible = false;

    protected get isVisible() {
        return this._cachedIsVisible;
    }

    protected get disposables() {
        return this._disposables;
    }

    get editor() {
        return this._editor;
    }

    constructor(editor: vs.TextEditor) {
        this._editor = editor;
    }

    dispose() {
        vs.Disposable.from(...this._disposables).dispose();
    }
}