import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/reactive/Events';
import { Observable } from 'rxjs';
import { Disposable } from 'vscode-jsonrpc';
import { PapyrusGame } from './PapyrusGame';
import { mergeMap, map } from 'rxjs/operators';

export interface IGameConfig {
    readonly enabled: boolean;
    readonly creationKitIniFiles: string[];
    readonly installPath: string;
}

export interface IExtensionConfig {
    readonly fallout4: IGameConfig;
    readonly skyrim: IGameConfig;
    readonly skyrimSpecialEdition: IGameConfig;
}

export interface IExtensionConfigProvider extends Disposable {
    readonly config: Observable<IExtensionConfig>;
    getConfigForGame(game: PapyrusGame): Promise<IGameConfig>;
}

function getPapyrusConfig() {
    return workspace.getConfiguration().get<IExtensionConfig>('papyrus');
}

export class ExtensionConfigProvider implements IExtensionConfigProvider {
    private readonly _config = eventToValueObservable(
        workspace.onDidChangeConfiguration,
        () => getPapyrusConfig(),
        (e) => (e.affectsConfiguration('papyrus') ? getPapyrusConfig() : undefined)
    );

    get config() {
        return this._config;
    }

    getConfigForGame(game: PapyrusGame) {
        return this._config.pipe(map((c) => c[game])).toPromise();
    }

    dispose() {}
}

export const IExtensionConfigProvider = createDecorator<IExtensionConfigProvider>('extensionConfigProvider');
