import { RequestType, TextDocumentIdentifier, TextDocumentRegistrationOptions } from 'vscode-languageclient';

export interface DocumentAssemblyParams {
    textDocument: TextDocumentIdentifier;
}

export interface DocumentAssembly {
    assembly: string;
}

export const documentAssemblyRequestType = {
    type: new RequestType<DocumentAssemblyParams, DocumentAssembly | null, void, TextDocumentRegistrationOptions>(
        'textDocument/assembly'
    ),
};
