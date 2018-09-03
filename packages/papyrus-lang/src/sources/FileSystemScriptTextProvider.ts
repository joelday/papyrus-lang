import { IFileSystem } from '../host/FileSystem';
import { IScriptTextProvider } from './ScriptTextProvider';

export class FileSystemScriptTextProvider implements IScriptTextProvider {
    private readonly _fileSystem: IFileSystem;

    constructor(@IFileSystem fileSystem: IFileSystem) {
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
