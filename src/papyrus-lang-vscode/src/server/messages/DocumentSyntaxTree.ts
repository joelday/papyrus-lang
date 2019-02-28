import {
    TextDocument,
    RequestType,
    TextDocumentIdentifier,
    TextDocumentRegistrationOptions,
    Range,
} from 'vscode-languageclient';
import { CancellationToken, ProviderResult } from 'vscode';

export interface DocumentSyntaxTreeParams {
    textDocument: TextDocumentIdentifier;
}

export interface DocumentSyntaxTree {
    root: DocumentSyntaxTreeNode;
}

export interface DocumentSyntaxTreeNode {
    name: string;
    text: string;
    children: DocumentSyntaxTreeNode[];
    range: Range;
}

export interface ProvideDocumentSyntaxTreeSignature {
    (document: TextDocument, token: CancellationToken): ProviderResult<DocumentSyntaxTree>;
}

export const documentSyntaxTreeRequestType = {
    type: new RequestType<DocumentSyntaxTreeParams, DocumentSyntaxTree | null, void, TextDocumentRegistrationOptions>(
        'textDocument/syntaxTree'
    ),
};
