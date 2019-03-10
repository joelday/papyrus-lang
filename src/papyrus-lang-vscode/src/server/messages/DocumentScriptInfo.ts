import { RequestType, TextDocumentIdentifier, TextDocumentRegistrationOptions } from 'vscode-languageclient';

export interface DocumentScriptInfoParams {
    textDocument: TextDocumentIdentifier;
}

export interface IdentifierFiles {
    identifer: string;
    files: string[];
}

export interface DocumentScriptInfo {
    identifiers: string[];
    identifierFiles: IdentifierFiles[];
    searchPaths: string[];
}

export const documentScriptInfoRequestType = {
    type: new RequestType<DocumentScriptInfoParams, DocumentScriptInfo | null, void, TextDocumentRegistrationOptions>(
        'textDocument/scriptInfo'
    ),
};
