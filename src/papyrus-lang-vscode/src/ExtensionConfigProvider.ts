import * as rx from 'rxjs';
import * as rxop from 'rxjs/operators';

import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/Reactive';

export interface IExtensionGameConfig {
    creationKitIniFiles: string[];
    installPath: string;
}

export interface IExtensionConfig {
    fallout4?: IExtensionGameConfig;
    skyrim?: IExtensionGameConfig;
    skyrimSpecialEdition?: IExtensionGameConfig;
}

export interface IExtensionConfigProvider {
    readonly config: rx.Observable<IExtensionConfig>;
}

export class ExtensionConfigProvider {
    private readonly _config = eventToValueObservable(
        workspace.onDidChangeConfiguration,
        () => workspace.getConfiguration('papyrus'),
        (e) => (e.affectsConfiguration('papyrus') ? workspace.getConfiguration('papyrus') : undefined)
    ).pipe(
        rxop.map(
            (workspaceConfig) =>
                ({
                    fallout4: workspaceConfig.get('fallout4'),
                    skyrim: workspaceConfig.get('skyrim'),
                    skyrimSpecialEdition: workspaceConfig.get('skyrimSpecialEdition'),
                } as IExtensionConfig)
        )
    );

    get config() {
        return this._config;
    }
}

export const IExtensionConfigProvider = createDecorator<IExtensionConfigProvider>('extensionConfigProvider');
