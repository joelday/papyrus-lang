import * as rx from 'rxjs';
import * as rxop from 'rxjs/operators';

import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/Reactive';
import { PapyrusGame } from './common/PapyrusGame';

export interface IGameConfig {
    creationKitIniFiles: string[];
    installPath: string;
}

export interface IExtensionConfig {
    fallout4: IGameConfig;
    skyrim: IGameConfig;
    skyrimSpecialEdition: IGameConfig;
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

    getConfigForGame(game: PapyrusGame) {
        return this._config.pipe(
            rxop.map((config) => {
                switch (game) {
                    case PapyrusGame.fallout4:
                        return config.fallout4;
                    case PapyrusGame.skyrim:
                        return config.skyrim;
                    case PapyrusGame.skyrimSpecialEdition:
                        return config.skyrimSpecialEdition;
                }
            })
        );
    }
}

export const IExtensionConfigProvider = createDecorator<IExtensionConfigProvider>('extensionConfigProvider');
