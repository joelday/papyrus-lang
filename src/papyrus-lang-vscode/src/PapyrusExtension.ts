import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService, Descriptor } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { LanguageClientManager } from './server/LanguageClientManager';
import { PapyrusGame } from './common/PapyrusGame';

class PapyrusExtension implements Disposable {
    private readonly _context: ExtensionContext;
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _clientManager: LanguageClientManager;

    constructor(context: ExtensionContext) {
        this._context = context;

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, new Descriptor(ExtensionConfigProvider)]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        this._clientManager = this._instantiationService.createInstance(LanguageClientManager);
    }

    dispose() {
        this._clientManager.dispose();
    }
}

let extension: PapyrusExtension;
export async function activate(context: ExtensionContext) {
    extension = new PapyrusExtension(context);
}

export function deactivate() {
    extension.dispose();
}
