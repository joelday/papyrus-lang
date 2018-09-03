import { FileSystem } from '../host/FileSystem';
import { ScriptTextProvider } from './ScriptTextProvider';

export class FileSystemScriptTextProvider implements ScriptTextProvider {
    private readonly _fileSystem: FileSystem;

    constructor(@FileSystem fileSystem: FileSystem) {
        this._fileSystem = fileSystem;
    }

    public getScriptText(uri: string) {
        return {
            text: this._fileSystem.readTextFile(uri),
            version: '0',
        };
    }

    public getScriptVersion(_uri: string): string {
        return '0';
    }
}
