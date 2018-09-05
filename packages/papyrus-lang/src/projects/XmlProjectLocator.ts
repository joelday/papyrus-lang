import { createDecorator } from 'decoration-ioc';
import * as path from 'upath';
import URI from 'vscode-uri';
import { IFileSystem } from '../host/FileSystem';

export class XmlProjectLocator {
    private readonly _fileSystem: IFileSystem;

    constructor(@IFileSystem fileSystem: IFileSystem) {
        this._fileSystem = fileSystem;
    }

    public findProjectFiles(rootUri: string) {
        return this._fileSystem.findFilesAsUris(
            path.normalize(path.join(URI.parse(rootUri).fsPath, '**', '*.ppj'))
        );
    }
}

export interface IXmlProjectLocator {
    findProjectFiles(rootUri: string): string[];
}

// tslint:disable-next-line:variable-name
export const IXmlProjectLocator = createDecorator<IXmlProjectLocator>(
    'xmlProjectLocator'
);
