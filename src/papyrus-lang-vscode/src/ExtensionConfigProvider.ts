import { injectable, interfaces } from 'inversify';
import { workspace, Disposable } from 'vscode';
import { eventToValueObservable } from './common/vscode/reactive/Events';
import { Observable } from 'rxjs';
export interface IGameConfig {
    readonly enabled: boolean;
    readonly creationKitIniFiles: string[];
    readonly installPath: string;
    readonly ignoreDebuggerVersion: boolean;
    readonly modDirectoryPath: string; // For supporting mod managers managing the debug plugin
}

export interface IExtensionConfig {
    readonly fallout4: IGameConfig;
    readonly skyrim: IGameConfig;
    readonly skyrimSpecialEdition: IGameConfig;
    readonly starfield: IGameConfig;
}

export interface IExtensionConfigProvider extends Disposable {
    readonly config: Observable<IExtensionConfig>;
}

function getPapyrusConfig() {
    return workspace.getConfiguration().get<IExtensionConfig>('papyrus');
}

@injectable()
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

export const IExtensionConfigProvider: interfaces.ServiceIdentifier<IExtensionConfigProvider> =
    Symbol('extensionConfigProvider');
