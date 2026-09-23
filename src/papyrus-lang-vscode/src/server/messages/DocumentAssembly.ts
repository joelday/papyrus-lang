import { RequestType, TextDocumentIdentifier } from 'vscode-languageclient';

export interface DocumentAssemblyParams {
    textDocument: TextDocumentIdentifier;
}

export interface DocumentAssembly {
    assembly: string;
}

export const documentAssemblyRequestType = {
    type: new RequestType<DocumentAssemblyParams, DocumentAssembly | null, void>('textDocument/assembly'),
};
