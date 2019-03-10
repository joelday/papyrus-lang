import { RequestType, TextDocumentIdentifier, TextDocumentRegistrationOptions, Range } from 'vscode-languageclient';

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

export const documentSyntaxTreeRequestType = {
    type: new RequestType<DocumentSyntaxTreeParams, DocumentSyntaxTree | null, void, TextDocumentRegistrationOptions>(
        'textDocument/syntaxTree'
    ),
};
