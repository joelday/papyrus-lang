import { createDecorator } from 'decoration-ioc';
import { findNodeAtPosition, Node, ScriptNode } from 'papyrus-lang/lib/parser/Node';
import { IScriptTextProvider } from 'papyrus-lang/lib/sources/ScriptTextProvider';
import { Position, TextDocument, TextDocuments } from 'vscode-languageserver';
import { ITextDocuments } from './External';
import { IProjectManager } from './ProjectManager';

export interface IDocumentHelpers {
    getTextDocument(documentUri: string): TextDocument;
    getScriptNode(documentUri: string): ScriptNode;
    getNodeAtPosition(documentUri: string, position: Position): Node;
}

export class DocumentHelpers implements IDocumentHelpers {
    private readonly _textDocuments: TextDocuments;
    private readonly _projectManager: IProjectManager;
    private readonly _scriptTextProvider: IScriptTextProvider;

    constructor(
        @ITextDocuments textDocuments: TextDocuments,
        @IProjectManager projectManager: IProjectManager,
        @IScriptTextProvider scriptTextProvider: IScriptTextProvider
    ) {
        this._textDocuments = textDocuments;
        this._projectManager = projectManager;
        this._scriptTextProvider = scriptTextProvider;
    }

    public getTextDocument(documentUri: string): TextDocument {
        return (
            this._textDocuments.get(documentUri) ||
            TextDocument.create(documentUri, 'papyrus', 0, this._scriptTextProvider.getScriptText(documentUri).text)
        );
    }

    public getScriptNode(documentUri: string): ScriptNode {
        const scriptFile = this._projectManager.getScriptFileByUri(documentUri);

        if (!scriptFile) {
            return null;
        }

        return scriptFile.scriptNode.scriptNode;
    }

    public getNodeAtPosition(documentUri: string, position: Position) {
        const textDocument = this.getTextDocument(documentUri);
        const scriptNode = this.getScriptNode(textDocument.uri);

        if (!scriptNode) {
            return null;
        }

        return findNodeAtPosition(scriptNode, textDocument.offsetAt(position));
    }
}

// tslint:disable-next-line:variable-name
export const IDocumentHelpers = createDecorator<IDocumentHelpers>('documentHelpers');
