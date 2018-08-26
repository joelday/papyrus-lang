import URI from 'vscode-uri';
import { readTextFile } from '../common/Utilities';

export interface ScriptText {
    text: string;
    version: string;
}

export interface LanguageServiceHost {
    getScriptText(uri: string): ScriptText;
    getScriptVersion(uri: string): string;
}

export class FileSystemLanguageServiceHost implements LanguageServiceHost {
    public getScriptVersion(_uri: string) {
        return '0';
    }

    public getScriptText(uri: string): ScriptText {
        return {
            version: '0',
            text: readTextFile(URI.parse(uri).fsPath),
        };
    }
}
