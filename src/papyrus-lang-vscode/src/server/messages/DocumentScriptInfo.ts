import {
    TextDocument,
    RequestType,
    TextDocumentIdentifier,
    TextDocumentRegistrationOptions,
} from 'vscode-languageclient';
import { CancellationToken, ProviderResult } from 'vscode';

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

export interface ProvideDocumentScriptInfoSignature {
    (document: TextDocument, token: CancellationToken): ProviderResult<DocumentScriptInfo>;
}

export const documentScriptInfoRequestType = {
    type: new RequestType<DocumentScriptInfoParams, DocumentScriptInfo | null, void, TextDocumentRegistrationOptions>(
        'textDocument/scriptInfo'
    ),
};
