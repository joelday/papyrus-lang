import * as iniParser from 'ini';

import { createDecorator } from 'decoration-ioc';
import { mergeAll } from 'lodash/fp';
import { iterateMany } from '../common/Utilities';
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
    loadInis(): CreationKitInis;
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

    public loadInis() {
        const locations = this._iniLocator.getIniLocations();

        return {
            creationKitInstallUri: locations.creationKitInstallUri,
            ini: mergeAll(
                Array.from(
                    iterateMany(
                        locations.iniUris.map((pattern) =>
                            this._fileSystem.findFilesAsUris(pattern)
                        )
                    )
                )
                    .map((iniUri) => [
                        iniUri,
                        this._fileSystem.readTextFile(iniUri),
                    ])
                    .map(
                        (iniText) =>
                            ({
                                ...iniParser.decode(iniText[1]),
                                iniFileUri: iniText[0],
                            } as CreationKitIni)
                    )
            ) as CreationKitIni,
        };
    }
}

// tslint:disable-next-line:variable-name
export const ICreationKitInisLoader = createDecorator<ICreationKitInisLoader>(
    'creationKitInisLoader'
);
