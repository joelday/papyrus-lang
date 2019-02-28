import * as rx from 'rxjs';
import * as rxop from 'rxjs/operators';

import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/Reactive';

export interface IPapyrusExtensionGameConfig {
    creationKitIniFiles: string[];
    installPath: string;
}

export interface IPapyrusExtensionConfig {
    fallout4?: IPapyrusExtensionGameConfig;
    skyrim?: IPapyrusExtensionGameConfig;
    skyrimSpecialEdition?: IPapyrusExtensionGameConfig;
}

export interface IPapyrusExtensionConfigProvider {
    readonly config: rx.Observable<IPapyrusExtensionConfig>;
}

export class PapyrusExtensionConfigProvider {
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
                } as IPapyrusExtensionConfig)
        )
    );

    get config() {
        return this._config;
    }
}

export const IPapyrusExtensionConfigProvider = createDecorator<IPapyrusExtensionConfigProvider>(
    'papyrusExtensionConfigProvider'
);
