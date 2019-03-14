import { createDecorator } from 'decoration-ioc';
import { workspace } from 'vscode';
import { eventToValueObservable } from './common/vscode/reactive/Events';
import { Observable } from 'rxjs';
import { Disposable } from 'vscode-jsonrpc';

export interface IGameConfig {
    enabled: boolean;
    creationKitIniFiles: string[];
    installPath: string;
    compilerPath: string;
}

export interface IExtensionConfig {
    fallout4: IGameConfig;
    skyrim: IGameConfig;
    skyrimSpecialEdition: IGameConfig;
}

export interface IExtensionConfigProvider extends Disposable {
    readonly config: Observable<IExtensionConfig>;
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

    dispose() {}
}

export const IExtensionConfigProvider = createDecorator<IExtensionConfigProvider>('extensionConfigProvider');
