import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';

class PapyrusExtension implements Disposable {
    private readonly _context: ExtensionContext;
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;

    constructor(context: ExtensionContext) {
        this._context = context;

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, ExtensionConfigProvider]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);
    }

    dispose() {}
}

let extension: PapyrusExtension;
export async function activate(context: ExtensionContext) {
    extension = new PapyrusExtension(context);
}

export function deactivate() {
    extension.dispose();
}
