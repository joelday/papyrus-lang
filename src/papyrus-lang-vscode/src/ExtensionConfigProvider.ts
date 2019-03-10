import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/Reactive';
import { PapyrusGame } from './PapyrusGame';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

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
    readonly config: Observable<IExtensionConfig>;
}

function getPapyrusConfig() {
    return workspace.getConfiguration().get<IExtensionConfig>('papyrus');
}

export class ExtensionConfigProvider {
    private readonly _config = eventToValueObservable(
        workspace.onDidChangeConfiguration,
        () => getPapyrusConfig(),
        (e) => (e.affectsConfiguration('papyrus') ? getPapyrusConfig() : undefined)
    );

    get config() {
        return this._config;
    }

    getConfigForGame(game: PapyrusGame) {
        return this._config.pipe(map((config) => config[game]));
    }
}

export const IExtensionConfigProvider = createDecorator<IExtensionConfigProvider>('extensionConfigProvider');
