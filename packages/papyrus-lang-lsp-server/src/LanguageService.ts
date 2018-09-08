import { Descriptor, IInstantiationService, InstantiationService, ServiceCollection } from 'decoration-ioc';
import { ServiceIdentifier } from 'decoration-ioc/lib/instantiation';
import { iterateMany } from 'papyrus-lang/lib/common/Utilities';
import { ICreationKitIniLocator } from 'papyrus-lang/lib/config/CreationKitIniLocator';
import { CreationKitInisLoader, ICreationKitInisLoader } from 'papyrus-lang/lib/config/CreationKitInisLoader';
import { IFileSystem } from 'papyrus-lang/lib/host/FileSystem';
import { NodeFileSystem } from 'papyrus-lang/lib/host/NodeFileSystem';
import { AmbientProjectLoader, IAmbientProjectLoader } from 'papyrus-lang/lib/projects/AmbientProjectLoader';
import { IXmlProjectConfigParser, XmlProjectConfigParser } from 'papyrus-lang/lib/projects/XmlProjectConfigParser';
import { IXmlProjectLoader, XmlProjectLoader } from 'papyrus-lang/lib/projects/XmlProjectLoader';
import { IXmlProjectLocator, XmlProjectLocator } from 'papyrus-lang/lib/projects/XmlProjectLocator';
import { IScriptTextProvider } from 'papyrus-lang/lib/sources/ScriptTextProvider';
import {
    ClientCapabilities,
    Connection,
    DidChangeConfigurationNotification,
    TextDocument,
    TextDocuments,
} from 'vscode-languageserver';
import { ConfigIniLocator } from './ConfigIniLocator';
import { ConfigProvider, IConfigProvider } from './ConfigProvider';
import { DocumentHelpers, IDocumentHelpers } from './DocumentHelpers';
import { ILanguageServerConnection, IRemoteConsole, ITextDocuments } from './External';
import { CompletionHandler } from './handlers/CompletionHandler';
import { CompletionResolveHandler } from './handlers/CompletionResolveHandler';
import { DefinitionHandler } from './handlers/DefinitionHandler';
import { DocumentSymbolHandler } from './handlers/DocumentSymbolHandler';
import { HoverHandler } from './handlers/HoverHandler';
import { SignatureHelpHandler } from './handlers/SignatureHelpHandler';
import { IProjectManager, ProjectManager } from './ProjectManager';
import { RequestHandlerService } from './RequestHandlerService';
import { TextDocumentScriptTextProvider } from './TextDocumentScriptTextProvider';

export class LanguageService {
    private readonly _connection: Connection;
    private readonly _textDocuments: TextDocuments;
    private readonly _capabilities: ClientCapabilities;
    private readonly _configProvider: ConfigProvider;
    private readonly _serviceCollection: ServiceCollection;
    private readonly _instantiationService: IInstantiationService;
    private readonly _projectManager: IProjectManager;
    private readonly _services: [ServiceIdentifier<any>, any][];

    constructor(connection: Connection, textDocuments: TextDocuments, capabilities: ClientCapabilities) {
        this._connection = connection;
        this._textDocuments = textDocuments;
        this._capabilities = capabilities;

        this._configProvider = new ConfigProvider();

        this._services = [
            [ILanguageServerConnection, connection],
            [IRemoteConsole, connection.console],
            [IConfigProvider, this._configProvider],
            [IFileSystem, new Descriptor(NodeFileSystem)],
            [ITextDocuments, this._textDocuments],
            [ICreationKitIniLocator, new Descriptor(ConfigIniLocator)],
            [ICreationKitInisLoader, new Descriptor(CreationKitInisLoader)],
            [IAmbientProjectLoader, new Descriptor(AmbientProjectLoader)],
            [IXmlProjectConfigParser, new Descriptor(XmlProjectConfigParser)],
            [IXmlProjectLoader, new Descriptor(XmlProjectLoader)],
            [IXmlProjectLocator, new Descriptor(XmlProjectLocator)],
            [IScriptTextProvider, new Descriptor(TextDocumentScriptTextProvider, this._textDocuments)],
            [IDocumentHelpers, new Descriptor(DocumentHelpers, this._textDocuments)],
            [IProjectManager, new Descriptor(ProjectManager)],
        ];

        this._serviceCollection = new ServiceCollection(...this._services);

        this._instantiationService = new InstantiationService(this._serviceCollection, false);
        this._projectManager = this._instantiationService.invokeFunction((accessor) => accessor.get(IProjectManager));

        if (this._capabilities.workspace && this._capabilities.workspace.configuration) {
            connection.onDidChangeConfiguration(async () => {
                this._configProvider.config = await connection.workspace.getConfiguration('papyrus');
                await this.updateProjects(true);
            });
        }

        this._connection.onInitialized(async () => {
            if (this._capabilities.workspace && this._capabilities.workspace.configuration) {
                await connection.client.register(DidChangeConfigurationNotification.type, { section: 'papyrus' });
                this._configProvider.config = await connection.workspace.getConfiguration('papyrus');
            }

            await this.updateProjects(false);

            this._textDocuments.onDidSave(async (change) => {
                if (change.document.languageId === 'papyrus-project') {
                    await this.updateProjects(true);
                }
            });

            // TODO: General solution for throttling:
            const pendingUpdates = new Map<string, NodeJS.Timer>();
            this._textDocuments.onDidChangeContent((change) => {
                if (pendingUpdates.has(change.document.uri)) {
                    clearTimeout(pendingUpdates.get(change.document.uri));
                }

                pendingUpdates.set(
                    change.document.uri,
                    setTimeout(() => {
                        try {
                            this.updateDiagnostics(change.document);
                        } finally {
                            pendingUpdates.delete(change.document.uri);
                        }
                    }, 1000)
                );
            });

            connection.onDidChangeWatchedFiles(async () => {
                await this.updateProjects(false);
            });

            textDocuments.onDidClose((e) => {
                connection.sendDiagnostics({ uri: e.document.uri, diagnostics: [] });
            });
        });

        this._instantiationService.createInstance(RequestHandlerService, [
            ['onCompletion', CompletionHandler],
            ['onCompletionResolve', CompletionResolveHandler],
            ['onDefinition', DefinitionHandler],
            ['onDocumentSymbol', DocumentSymbolHandler],
            ['onHover', HoverHandler],
            ['onSignatureHelp', SignatureHelpHandler],
        ]);
    }

    public toString() {
        return `Papyrus language service:\r\n    Registered services:\r\n${this._services
            .map((s) => `        ${s[0]}: ${s[1]}`)
            .join('\r\n')}`;
    }

    private async updateProjects(reloadExisting: boolean) {
        const folders = await this._connection.workspace.getWorkspaceFolders();
        this._projectManager.updateProjects(folders.map((f) => f.uri), reloadExisting);

        for (const document of this._textDocuments.all()) {
            this.updateDiagnostics(document);
        }
    }

    private updateDiagnostics(document: TextDocument) {
        const diagnostics = Array.from(
            iterateMany(this._projectManager.projectHosts.map((host) => host.getDiagnosticsForDocument(document)))
        );

        this._connection.sendDiagnostics({
            uri: document.uri,
            diagnostics,
        });
    }
}
