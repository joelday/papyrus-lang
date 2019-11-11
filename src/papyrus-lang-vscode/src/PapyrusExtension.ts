import { Disposable, extensions, ExtensionContext, workspace, window, TreeDataProvider, TreeItem, commands } from 'vscode';
import { ServiceCollection, IInstantiationService, InstantiationService, Descriptor } from 'decoration-ioc';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { extensionQualifiedId, GlobalState } from './common/constants';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { LanguageClientManager, ILanguageClientManager } from './server/LanguageClientManager';
import { LanguageServiceStatusItems } from './features/LanguageServiceStatusItems';
import { LanguageConfigurations } from './features/LanguageConfigurations';
import { getInstance } from './common/Ioc';
import { PyroTaskProvider } from './features/PyroTaskProvider';
import { ICreationKitInfoProvider, CreationKitInfoProvider } from './CreationKitInfoProvider';
import { ScriptStatusCodeLensProvider } from './features/ScriptStatusCodeLensProvider';
import { SearchCreationKitWikiCommand } from './features/commands/SearchCreationKitWikiCommand';
import { PapyrusDebugConfigurationProvider } from './debugger/PapyrusDebugConfigurationProvider';
import { PapyrusDebugAdapterDescriptorFactory } from './debugger/PapyrusDebugAdapterDescriptorFactory';
import { IDebugSupportInstallService, DebugSupportInstallService } from './debugger/DebugSupportInstallService';
import { InstallDebugSupportCommand } from './features/commands/InstallDebugSupportCommand';
import { PapyrusDebugAdapterTrackerFactory } from './debugger/PapyrusDebugAdapterTracker';
import { AttachDebuggerCommand } from './features/commands/AttachDebuggerCommand';
import { ProjectsView } from './features/projects/ProjectsView';
import { ProjectsTreeDataProvider } from './features/projects/ProjectsTreeDataProvider';
import { AssemblyTextContentProvider } from './features/AssemblyTextContentProvider';
import { ViewAssemblyCommand } from './features/commands/ViewAssemblyCommand';
import { GenerateProjectCommand } from './features/commands/GenerateProjectCommand';
import { showWelcome } from './features/WelcomeHandler';
import { ShowWelcomeCommand } from './features/commands/ShowWelcomeCommand';

class PapyrusExtension implements Disposable {
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _clientManager: ILanguageClientManager;
    private readonly _statusItems: LanguageServiceStatusItems;
    private readonly _languageConfigurations: LanguageConfigurations;
    private readonly _pyroProvider: PyroTaskProvider;
    private readonly _scriptStatusCodeLensProvider: ScriptStatusCodeLensProvider;
    private readonly _searchWikiCommand: SearchCreationKitWikiCommand;
    private readonly _debugConfigurationProvider: PapyrusDebugConfigurationProvider;
    private readonly _debugAdapterDescriptorFactory: PapyrusDebugAdapterDescriptorFactory;
    private readonly _installDebugSupportCommand: InstallDebugSupportCommand;
    private readonly _debugAdapterTrackerFactory: PapyrusDebugAdapterTrackerFactory;
    private readonly _attachCommand: AttachDebuggerCommand;
    private readonly _projectsTreeDataProvider: ProjectsTreeDataProvider;
    private readonly _projectsView: ProjectsView;
    private readonly _assemblyTextContentProvider: AssemblyTextContentProvider;
    private readonly _viewAssemblyCommand: ViewAssemblyCommand;
    private readonly _generateProjectCommand: GenerateProjectCommand;
    private readonly _showWelcomeCommand: ShowWelcomeCommand;

    constructor(context: ExtensionContext) {

        // This comes first just in case anything below needs to change based on version upgrades
        const papyrus = extensions.getExtension(extensionQualifiedId)!;
        const papyrusVersion = papyrus.packageJSON.version;
        const previousVersion = context.globalState.get<string>(GlobalState.PapyrusVersion);

        this._languageConfigurations = new LanguageConfigurations();

        this._serviceCollection = new ServiceCollection(
            [IExtensionContext, context],
            [IExtensionConfigProvider, new Descriptor(ExtensionConfigProvider)],
            [ICreationKitInfoProvider, new Descriptor(CreationKitInfoProvider)],
            [ILanguageClientManager, new Descriptor(LanguageClientManager)],
            [IDebugSupportInstallService, new Descriptor(DebugSupportInstallService)],
        );

        this._instantiationService = new InstantiationService(this._serviceCollection);

        this._configProvider = getInstance(this._serviceCollection, IExtensionConfigProvider);
        this._clientManager = getInstance(this._serviceCollection, ILanguageClientManager);
        this._statusItems = this._instantiationService.createInstance(LanguageServiceStatusItems);

        this._pyroProvider = this._instantiationService.createInstance(PyroTaskProvider);

        this._scriptStatusCodeLensProvider = this._instantiationService.createInstance(ScriptStatusCodeLensProvider);
        this._searchWikiCommand = this._instantiationService.createInstance(SearchCreationKitWikiCommand);

        this._debugConfigurationProvider = this._instantiationService.createInstance(PapyrusDebugConfigurationProvider);
        this._debugAdapterDescriptorFactory = this._instantiationService.createInstance(
            PapyrusDebugAdapterDescriptorFactory
        );

        this._installDebugSupportCommand = this._instantiationService.createInstance(InstallDebugSupportCommand);
        this._debugAdapterTrackerFactory = new PapyrusDebugAdapterTrackerFactory();

        this._attachCommand = new AttachDebuggerCommand();

        this._projectsTreeDataProvider = this._instantiationService.createInstance(ProjectsTreeDataProvider);
        this._projectsView = new ProjectsView(this._projectsTreeDataProvider);

        this._assemblyTextContentProvider = this._instantiationService.createInstance(AssemblyTextContentProvider);
        this._viewAssemblyCommand = this._instantiationService.createInstance(ViewAssemblyCommand);

        this._generateProjectCommand = this._instantiationService.createInstance(GenerateProjectCommand);

        this._showWelcomeCommand = this._instantiationService.createInstance(ShowWelcomeCommand);

        // Show the getting started document if there's no previous version (new install)
        // At some point we might want a "what's new" so I included both version numbers.
        // void showWelcome(papyrusVersion, previousVersion);
        void showWelcome(papyrusVersion, undefined);
        context.globalState.update(GlobalState.PapyrusVersion, papyrusVersion);
    }

    dispose() {
        this._showWelcomeCommand.dispose();

        this._generateProjectCommand.dispose();

        this._viewAssemblyCommand.dispose();
        this._assemblyTextContentProvider.dispose();

        this._projectsView.dispose();
        this._projectsTreeDataProvider.dispose();

        this._attachCommand.dispose();

        this._debugAdapterTrackerFactory.dispose();

        this._installDebugSupportCommand.dispose();

        this._debugAdapterDescriptorFactory.dispose();
        this._debugConfigurationProvider.dispose();

        this._searchWikiCommand.dispose();
        this._scriptStatusCodeLensProvider.dispose();

        this._pyroProvider.dispose();

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
