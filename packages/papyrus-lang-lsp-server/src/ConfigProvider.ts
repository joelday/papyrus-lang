import { createDecorator } from 'decoration-ioc';

export class ConfigProvider implements IConfigProvider {
    public config: IPapyrusLangConfig = {};
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
