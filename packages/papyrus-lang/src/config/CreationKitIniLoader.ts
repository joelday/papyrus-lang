import { IFileSystem } from '../host/FileSystem';
import { CreationKitIniParser } from './CreationKitIniParser';

export class CreationKitIniLoader {
    private readonly _fileSystem: IFileSystem;
    private readonly _parser: CreationKitIniParser;

    constructor(@IFileSystem fileSystem: IFileSystem) {
        this._fileSystem = fileSystem;
        this._parser = new CreationKitIniParser();
    }

    public loadInis(iniUris: string[]) {

    }
}
