import * as ini from 'ini';
import { mapKeys } from 'lodash/fp';

export interface CreationKitIni {
    papyrus?: {
        sscriptsourcefolder?: string;
        sadditionalimports?: string;
    };
}

export class CreationKitIniParser {
    public parseIni(iniText: string): CreationKitIni {
        const creationKitIni = ini.decode(iniText);
        return mapKeys(k => k.toLowerCase(), creationKitIni);
    }
}
