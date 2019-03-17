import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService, Descriptor } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { LanguageClientManager, ILanguageClientManager } from './server/LanguageClientManager';
import * as vscode from 'vscode';
import { LanguageServiceStatusItems } from './features/LanguageServiceStatusItems';
import { LanguageConfigurations } from './features/LanguageConfigurations';
import { getInstance } from './common/Ioc';
import { CompilerTaskProvider } from './features/CompilerTaskProvider';
import { ICreationKitInfoProvider, CreationKitInfoProvider } from './CreationKitInfoProvider';

class PapyrusExtension implements Disposable {
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _clientManager: ILanguageClientManager;
    private readonly _statusItems: LanguageServiceStatusItems;
    private readonly _languageConfigurations: LanguageConfigurations;
    private readonly _taskProvider: CompilerTaskProvider;

    constructor(context: ExtensionContext) {
        this._languageConfigurations = new LanguageConfigurations();

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, new Descriptor(ExtensionConfigProvider)],
            [ICreationKitInfoProvider, new Descriptor(CreationKitInfoProvider)],
            [ILanguageClientManager, new Descriptor(LanguageClientManager)]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        this._configProvider = getInstance(this._serviceCollection, IExtensionConfigProvider);
        this._clientManager = getInstance(this._serviceCollection, ILanguageClientManager);

        this._statusItems = this._instantiationService.createInstance(LanguageServiceStatusItems);
        this._taskProvider = this._instantiationService.createInstance(CompilerTaskProvider);
    }

    dispose() {
        this._taskProvider.dispose();
        this._statusItems.dispose();
        this._clientManager.dispose();
        this._configProvider.dispose();
        this._languageConfigurations.dispose();
    }
}

export async function activate(context: ExtensionContext) {
    const extension = new PapyrusExtension(context);
    context.subscriptions.push(extension);
}
