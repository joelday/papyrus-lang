import * as iniParser from 'ini';

import { createDecorator } from 'decoration-ioc';
import { merge } from 'lodash';
import * as mapKeysDeep from 'map-keys-deep-lodash';
import { IFileSystem } from '../host/FileSystem';
import { ICreationKitIniLocator } from './CreationKitIniLocator';

export interface CreationKitIni {
    iniFileUri: string;
    papyrus?: {
        sscriptsourcefolder?: string;
        sadditionalimports?: string;
    };
}

export interface CreationKitInis {
    creationKitInstallUri: string;
    ini: CreationKitIni;
}

export interface ICreationKitInisLoader {
    loadInis(workspaceUri: string): CreationKitInis;
}

export class CreationKitInisLoader implements ICreationKitInisLoader {
    private readonly _iniLocator: ICreationKitIniLocator;
    private readonly _fileSystem: IFileSystem;

    constructor(
        @ICreationKitIniLocator iniLocator: ICreationKitIniLocator,
        @IFileSystem fileSystem: IFileSystem
    ) {
        this._iniLocator = iniLocator;
        this._fileSystem = fileSystem;
    }

    public loadInis(workspaceUri: string) {
        const locations = this._iniLocator.getIniLocations(workspaceUri);

        return {
            creationKitInstallUri: locations.creationKitInstallUri,
            ini: merge(
                {},
                ...locations.iniUris
                    .filter((iniUri) => this._fileSystem.uriExists(iniUri))
                    .map((iniUri) => [
                        iniUri,
                        this._fileSystem.readTextFile(iniUri),
                    ])
                    .map((iniText) => {
                        return {
                            ...this.parseIni(iniText[1]),
                            iniFileUri: iniText[0],
                        } as CreationKitIni;
                    })
            ) as CreationKitIni,
        };
    }

    private parseIni(iniText: string) {
        return mapKeysDeep(iniParser.decode(iniText), (_v, k) =>
            k.toLowerCase()
        );
    }
}

// tslint:disable-next-line:variable-name
export const ICreationKitInisLoader = createDecorator<ICreationKitInisLoader>(
    'creationKitInisLoader'
);
