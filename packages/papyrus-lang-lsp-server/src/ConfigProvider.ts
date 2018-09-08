import { createDecorator } from 'decoration-ioc';
import { merge } from 'lodash';

const defaultConfig: IPapyrusLangConfig = {
    fallout4: {
        installPath: 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Fallout 4',
        creationKitIniFiles: ['CreationKit.ini', 'CreationKitCustom.ini'],
    },
};

export class ConfigProvider implements IConfigProvider {
    private _config: IPapyrusLangConfig = defaultConfig;

    get config() {
        return this._config;
    }

    set config(config: IPapyrusLangConfig) {
        this._config = merge({}, defaultConfig, config);
    }
}

export interface IPapyrusLangConfig {
    fallout4?: {
        installPath?: string;
        creationKitIniFiles?: string[];
    };
}

export interface IConfigProvider {
    readonly config: IPapyrusLangConfig;
}

// tslint:disable-next-line:variable-name
export const IConfigProvider = createDecorator<IConfigProvider>('configProvider');
