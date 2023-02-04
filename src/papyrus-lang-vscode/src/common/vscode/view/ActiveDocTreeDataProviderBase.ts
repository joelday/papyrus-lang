import { injectable } from 'inversify';
import * as vs from 'vscode';

import { TreeDataProviderBase } from './TreeDataProviderBase';

@injectable()
export abstract class ActiveDocTreeDataProviderBase extends TreeDataProviderBase {
    constructor() {
        super();

        vs.window.onDidChangeActiveTextEditor(() => this.refresh(), this.disposables);
        vs.workspace.onDidChangeTextDocument((e) => {
            if (
                this.currentDocument &&
                this.currentDocument.fileName.toLowerCase() === e.document.fileName.toLowerCase()
            ) {
                this.refresh();
            }
        });
    }

    protected get currentDocument() {
        return vs.window.activeTextEditor ? vs.window.activeTextEditor.document : null;
    }

    protected abstract refresh(): void;
}
