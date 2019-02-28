import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IPapyrusExtensionConfigProvider, PapyrusExtensionConfigProvider } from './PapyrusExtensionConfigProvider';
import { Subscription } from 'rxjs';

class PapyrusExtension implements Disposable {
    private readonly _context: ExtensionContext;
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _configSubscription: Subscription;

    constructor(context: ExtensionContext) {
        this._context = context;

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IPapyrusExtensionConfigProvider, PapyrusExtensionConfigProvider]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        const configProvider = this._instantiationService.createInstance(PapyrusExtensionConfigProvider);

        configProvider.config.subscribe({
            next: (c) => {
                console.log(c);
            },
        });
    }

    dispose() {
        this._configSubscription.unsubscribe();
    }
}

let extension: PapyrusExtension;
export async function activate(context: ExtensionContext) {
    extension = new PapyrusExtension(context);
}

export function deactivate() {
    extension.dispose();
}
