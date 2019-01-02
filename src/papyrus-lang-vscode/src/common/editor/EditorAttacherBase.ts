import * as vs from 'vscode';

import { EditorAttachmentBase } from './EditorAttachmentBase';

export abstract class EditorAttacherBase<T extends EditorAttachmentBase> implements vs.Disposable {
    private readonly _disposables: vs.Disposable[] = [];
    private readonly _mapping = new Map<vs.TextEditor, T>();

    constructor() {
        vs.window.onDidChangeVisibleTextEditors((editors) => {
            this.updateAttachments(editors);
        }, undefined, this._disposables);
    }

    updateAttachments(editors = vs.window.visibleTextEditors) {
        const missingEditors = Array.from(this._mapping.keys()).filter((e) => editors.indexOf(e) === -1);
        const unattachedEditors = editors.filter((e) => !this._mapping.has(e));

        for (const missingEditor of missingEditors) {
            this._mapping.get(missingEditor)!.dispose();
            this._mapping.delete(missingEditor);
        }

        for (const unattachedEditor of unattachedEditors) {
            if (this.shouldAttach(unattachedEditor)) {
                this._mapping.set(unattachedEditor, this.createAttachment(unattachedEditor));
            }
        }
    }

    dispose() {
        vs.Disposable.from(...this._disposables, ...Array.from(this._mapping.values())).dispose();
        this._mapping.clear();
    }

    protected get attachments(): ReadonlyArray<T> {
        return Array.from(this._mapping.values());
    }

    protected abstract shouldAttach(editor: vs.TextEditor): boolean;
    protected abstract createAttachment(editor: vs.TextEditor): T;
}
