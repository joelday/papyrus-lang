import { Disposable, ExtensionContext } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService, Descriptor } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { LanguageClientManager, ILanguageClientManager } from './server/LanguageClientManager';
import { LanguageServiceStatusItems } from './features/LanguageServiceStatusItems';
import { LanguageConfigurations } from './features/LanguageConfigurations';
import { getInstance } from './common/Ioc';
// import { CompilerTaskProvider } from './features/CompilerTaskProvider';
import { ICreationKitInfoProvider, CreationKitInfoProvider } from './CreationKitInfoProvider';
import { ScriptStatusCodeLensProvider } from './features/ScriptStatusCodeLensProvider';
import { SearchCreationKitWikiCommand } from './features/SearchCreationKitWikiCommand';
import { PapyrusDebugConfigurationProvider } from './debugger/PapyrusDebugConfigurationProvider';
import { PapyrusDebugAdapterDescriptorFactory } from './debugger/PapyrusDebugAdapterDescriptorFactory';
import { IDebugSupportInstaller, DebugSupportInstaller } from './debugger/DebugSupportInstaller';
import { InstallDebugSupportCommand } from './features/commands/InstallDebugSupportCommand';
import { PapyrusDebugAdapterTrackerFactory } from './debugger/PapyrusDebugAdapterTracker';

class PapyrusExtension implements Disposable {
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _clientManager: ILanguageClientManager;
    private readonly _statusItems: LanguageServiceStatusItems;
    private readonly _languageConfigurations: LanguageConfigurations;
    // private readonly _taskProvider: CompilerTaskProvider;
    private readonly _scriptStatusCodeLensProvider: ScriptStatusCodeLensProvider;
    private readonly _searchWikiCommand: SearchCreationKitWikiCommand;
    private readonly _debugConfigurationProvider: PapyrusDebugConfigurationProvider;
    private readonly _debugAdapterDescriptorFactory: PapyrusDebugAdapterDescriptorFactory;
    private readonly _installDebugSupportCommand: InstallDebugSupportCommand;
    private readonly _debugAdapterTrackerFactory: PapyrusDebugAdapterTrackerFactory;

    constructor(context: ExtensionContext) {
        this._languageConfigurations = new LanguageConfigurations();

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, new Descriptor(ExtensionConfigProvider)],
            [ICreationKitInfoProvider, new Descriptor(CreationKitInfoProvider)],
            [ILanguageClientManager, new Descriptor(LanguageClientManager)],
            [IDebugSupportInstaller, new Descriptor(DebugSupportInstaller)]
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        this._configProvider = getInstance(this._serviceCollection, IExtensionConfigProvider);
        this._clientManager = getInstance(this._serviceCollection, ILanguageClientManager);
        this._statusItems = this._instantiationService.createInstance(LanguageServiceStatusItems);

        // this._taskProvider = this._instantiationService.createInstance(CompilerTaskProvider);

        this._scriptStatusCodeLensProvider = this._instantiationService.createInstance(ScriptStatusCodeLensProvider);
        this._searchWikiCommand = this._instantiationService.createInstance(SearchCreationKitWikiCommand);

        this._debugConfigurationProvider = this._instantiationService.createInstance(PapyrusDebugConfigurationProvider);
        this._debugAdapterDescriptorFactory = this._instantiationService.createInstance(
            PapyrusDebugAdapterDescriptorFactory
        );

        this._installDebugSupportCommand = this._instantiationService.createInstance(InstallDebugSupportCommand);
        this._debugAdapterTrackerFactory = new PapyrusDebugAdapterTrackerFactory();
    }

    dispose() {
        this._debugAdapterTrackerFactory.dispose();

        this._installDebugSupportCommand.dispose();

        this._debugAdapterDescriptorFactory.dispose();
        this._debugConfigurationProvider.dispose();

        this._searchWikiCommand.dispose();
        this._scriptStatusCodeLensProvider.dispose();

        // this._taskProvider.dispose();

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
