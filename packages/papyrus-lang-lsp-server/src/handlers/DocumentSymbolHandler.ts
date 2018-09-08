import { CancellationToken, DocumentSymbolParams } from 'vscode-languageserver';
import { IDocumentHelpers } from '../DocumentHelpers';
import { getDocumentSymbolTree } from '../features/Symbols';
import { Handler } from '../RequestHandlerService';

export class DocumentSymbolHandler implements Handler<'onDocumentSymbol'> {
    private readonly _documentHelpers: IDocumentHelpers;

    constructor(@IDocumentHelpers documentHelpers: IDocumentHelpers) {
        this._documentHelpers = documentHelpers;
    }

    public handleRequest(params: DocumentSymbolParams, _cancellationToken: CancellationToken) {
        const scriptNode = this._documentHelpers.getScriptNode(params.textDocument.uri);

        return [
            getDocumentSymbolTree(scriptNode.symbol, this._documentHelpers.getTextDocument(params.textDocument.uri)),
        ];
    }
}
