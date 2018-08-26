import {
    FileSystemLanguageServiceHost,
    LanguageServiceHost,
    ScriptText,
} from 'papyrus-lang/lib/program/LanguageServiceHost';
import { TextDocument, TextDocuments } from 'vscode-languageserver';

export class TextDocumentLanguageServiceHost implements LanguageServiceHost {
    private readonly _fileLanguageServiceHost: LanguageServiceHost = new FileSystemLanguageServiceHost();
    private readonly _textDocuments: TextDocuments;

    constructor(textDocuments: TextDocuments) {
        this._textDocuments = textDocuments;
    }

    public getTextDocument(uri: string) {
        return (
            this._textDocuments.get(uri) ||
            TextDocument.create(uri, 'papyrus', 0, this.getScriptText(uri).text)
        );
    }

    public getScriptText(uri: string): ScriptText {
        const document = this._textDocuments.get(uri);
        if (!document) {
            return this._fileLanguageServiceHost.getScriptText(uri);
        }

        return {
            text: document.getText(),
            version: document.version.toString(),
        };
    }

    public getScriptVersion(uri: string) {
        const document = this._textDocuments.get(uri);
        if (!document) {
            return this._fileLanguageServiceHost.getScriptVersion(uri);
        }

        return document.version.toString();
    }
}
