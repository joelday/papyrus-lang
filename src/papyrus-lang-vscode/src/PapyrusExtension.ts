import 'reflect-metadata';

import { Disposable, extensions, ExtensionContext } from 'vscode';
import { extensionQualifiedId, GlobalState } from './common/constants';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { IExtensionConfigProvider, ExtensionConfigProvider } from './ExtensionConfigProvider';
import { IPathResolver, PathResolver } from './common/PathResolver';
import { LanguageClientManager, ILanguageClientManager } from './server/LanguageClientManager';
import { LanguageServiceStatusItems } from './features/LanguageServiceStatusItems';
import { LanguageConfigurations } from './features/LanguageConfigurations';
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
import { Container } from 'inversify';
import { IDebugLauncherService, DebugLauncherService } from './debugger/DebugLauncherService';
import { IAddressLibraryInstallService, AddressLibraryInstallService } from './debugger/AddressLibInstallService';
import { IMO2LaunchDescriptorFactory, MO2LaunchDescriptorFactory } from './debugger/MO2LaunchDescriptorFactory';
import { IMO2ConfiguratorService, MO2ConfiguratorService } from './debugger/MO2ConfiguratorService';
import { IGameDebugConfiguratorService, GameDebugConfiguratorService } from './debugger/GameDebugConfiguratorService';

class PapyrusExtension implements Disposable {
    private readonly _serviceContainer: Container;
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

        this._serviceContainer = new Container({
            defaultScope: 'Singleton',
        });

        this._serviceContainer.bind(IExtensionContext).toConstantValue(context);
        this._serviceContainer.bind(IExtensionConfigProvider).to(ExtensionConfigProvider);
        this._serviceContainer.bind(IPathResolver).to(PathResolver);
        this._serviceContainer.bind(ICreationKitInfoProvider).to(CreationKitInfoProvider);
        this._serviceContainer.bind(ILanguageClientManager).to(LanguageClientManager);
        this._serviceContainer.bind(IDebugSupportInstallService).to(DebugSupportInstallService);
        this._serviceContainer.bind(IDebugLauncherService).to(DebugLauncherService);
        this._serviceContainer.bind(IAddressLibraryInstallService).to(AddressLibraryInstallService);
        this._serviceContainer.bind(IMO2LaunchDescriptorFactory).to(MO2LaunchDescriptorFactory);
        this._serviceContainer.bind(IMO2ConfiguratorService).to(MO2ConfiguratorService);
        this._serviceContainer.bind(IGameDebugConfiguratorService).to(GameDebugConfiguratorService);

        this._configProvider = this._serviceContainer.get(IExtensionConfigProvider);
        this._clientManager = this._serviceContainer.get(ILanguageClientManager);

        this._statusItems = this._serviceContainer.resolve(LanguageServiceStatusItems);
        this._pyroProvider = this._serviceContainer.resolve(PyroTaskProvider);
        this._scriptStatusCodeLensProvider = this._serviceContainer.resolve(ScriptStatusCodeLensProvider);
        this._searchWikiCommand = this._serviceContainer.resolve(SearchCreationKitWikiCommand);
        this._debugConfigurationProvider = this._serviceContainer.resolve(PapyrusDebugConfigurationProvider);
        this._debugAdapterDescriptorFactory = this._serviceContainer.resolve(PapyrusDebugAdapterDescriptorFactory);
        this._installDebugSupportCommand = this._serviceContainer.resolve(InstallDebugSupportCommand);

        this._debugAdapterTrackerFactory = this._serviceContainer.resolve(PapyrusDebugAdapterTrackerFactory);

        this._attachCommand = new AttachDebuggerCommand();

        this._projectsTreeDataProvider = this._serviceContainer.resolve(ProjectsTreeDataProvider);
        this._projectsView = new ProjectsView(this._projectsTreeDataProvider);

        this._assemblyTextContentProvider = this._serviceContainer.resolve(AssemblyTextContentProvider);
        this._viewAssemblyCommand = this._serviceContainer.resolve(ViewAssemblyCommand);

        this._generateProjectCommand = this._serviceContainer.resolve(GenerateProjectCommand);

        this._showWelcomeCommand = this._serviceContainer.resolve(ShowWelcomeCommand);

        void showWelcome(papyrusVersion, previousVersion);
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
