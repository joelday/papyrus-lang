import { IInstantiationService } from 'decoration-ioc';
import { FileSystemScriptTextProvider } from 'papyrus-lang/lib/sources/FileSystemScriptTextProvider';
import {
    IScriptTextProvider,
    ScriptText,
} from 'papyrus-lang/lib/sources/ScriptTextProvider';
import { TextDocument, TextDocuments } from 'vscode-languageserver';

export class TextDocumentScriptTextProvider implements IScriptTextProvider {
    private readonly _baseScriptTextProvider: IScriptTextProvider;
    private readonly _textDocuments: TextDocuments;

    constructor(
        textDocuments: TextDocuments,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        this._textDocuments = textDocuments;
        this._baseScriptTextProvider = instantiationService.createInstance(
            FileSystemScriptTextProvider
        );
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
            return this._baseScriptTextProvider.getScriptText(uri);
        }

        return {
            text: document.getText(),
            version: document.version.toString(),
        };
    }

    public getScriptVersion(uri: string) {
        const document = this._textDocuments.get(uri);
        if (!document) {
            return this._baseScriptTextProvider.getScriptVersion(uri);
        }

        return document.version.toString();
    }
}
