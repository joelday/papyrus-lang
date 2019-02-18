import { FileSystemWatcher, workspace, CancellationToken, ProviderResult } from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    Trace,
    TextDocument,
    RequestType,
    TextDocumentIdentifier,
    TextDocumentRegistrationOptions,
    Range,
} from 'vscode-languageclient';

import psList from 'ps-list';
import { exec } from 'child_process';

function getServerExecution(toolPath: string, compilerAssemblyPath: string) {
    const args = ['--compilerAssemblyPath', compilerAssemblyPath];

    if (process.platform !== 'win32') {
        return {
            command: 'mono',
            args: [toolPath, ...args],
        };
    } else {
        return { command: toolPath, args };
    }
}

export interface DocumentSyntaxTreeParams {
    textDocument: TextDocumentIdentifier;
}

export interface DocumentSyntaxTree {
    root: DocumentSyntaxTreeNode;
}

export interface DocumentSyntaxTreeNode {
    name: string;
    text: string;
    children: DocumentSyntaxTreeNode[];
    range: Range;
}

export interface ProvideDocumentSyntaxTreeSignature {
    (document: TextDocument, token: CancellationToken): ProviderResult<DocumentSyntaxTree>;
}

export const documentSyntaxTreeRequestType = {
    type: new RequestType<DocumentSyntaxTreeParams, DocumentSyntaxTree | null, void, TextDocumentRegistrationOptions>(
        'textDocument/syntaxTree'
    ),
};

export interface DocumentScriptInfoParams {
    textDocument: TextDocumentIdentifier;
}

export interface IdentifierFiles {
    identifer: string;
    files: string[];
}

export interface DocumentScriptInfo {
    identifiers: string[];
    identifierFiles: IdentifierFiles[];
    searchPaths: string[];
}

export interface ProvideDocumentScriptInfoSignature {
    (document: TextDocument, token: CancellationToken): ProviderResult<DocumentScriptInfo>;
}

export const documentScriptInfoRequestType = {
    type: new RequestType<DocumentScriptInfoParams, DocumentScriptInfo | null, void, TextDocumentRegistrationOptions>(
        'textDocument/scriptInfo'
    ),
};

export interface RegistryInstallPathParams {
    RegKeyName: string;
}

export interface RegistryInstallPathInfo {
    value: string;
    exists: boolean;
}

export const documentRegistryInstallPathRequestType = {
    type: new RequestType<RegistryInstallPathParams, RegistryInstallPathInfo | null, void, TextDocumentRegistrationOptions>(
        'textDocument/registryInstallPath'
    ),
};

export class ClientServer {
    private readonly _serverOptions: ServerOptions;
    private _client: LanguageClient;
    private _fsWatcher: FileSystemWatcher;

    constructor(toolPath: string, compilerAssemblyPath: string) {
        const serverExecution = getServerExecution(toolPath, compilerAssemblyPath);

        this._serverOptions = {
            run: serverExecution,
            debug: serverExecution,
        };
    }

    /// examples
    // let Result = await clientServer.requestRegistryInstallPath('Fallout4');
    // let Result = await clientServer.requestRegistryInstallPath('Skyrim');
    // let Result = await clientServer.requestRegistryInstallPath('Skyrim Special Edition');
    public requestRegistryInstallPath(KeyName: string): Thenable<RegistryInstallPathInfo> {
        return this._client.sendRequest(documentRegistryInstallPathRequestType.type, {
            RegKeyName: KeyName,
        });
    }

    public requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree> {
        return this._client.sendRequest(documentSyntaxTreeRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    public requestScriptInfo(uri: string): Thenable<DocumentScriptInfo> {
        return this._client.sendRequest(documentScriptInfoRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    public isActive(): boolean {
        if (this._client) {
            return true;
        }
        return false;
    }

    public async start() {
        if (this._client) {
            return;
        }

        this._fsWatcher = workspace.createFileSystemWatcher('**/*.{flg,ppj,psc}');

        const clientOptions: LanguageClientOptions = {
            documentSelector: [
                { scheme: 'file', language: 'papyrus' },
                { scheme: 'file', language: 'papyrus-project' },
            ],
            synchronize: {
                fileEvents: this._fsWatcher,
                configurationSection: 'papyrus',
            },
        };

        // In order to find the new process id, we need to exclude any current running instances of the language server.
        let existingProcessIds: number[];
        if (process.platform === 'win32' && process.env['PAPYRUS_EXTENSION_DEBUG']) {
            const processes = await psList();
            existingProcessIds = processes.filter((p) => p.name.startsWith('DarkId.Papyrus.Host')).map((p) => p.pid);
        }

        // Create the language client and start the client.
        this._client = new LanguageClient('papyrus', 'Papyrus Language Service', this._serverOptions, clientOptions);

        this._client.trace = Trace.Verbose;
        this._client.start();

        await this._client.onReady();

        if (process.platform === 'win32' && process.env['PAPYRUS_EXTENSION_DEBUG']) {
            const processes = await psList();
            const serverProcessId = processes.find(
                (p) => p.name.startsWith('DarkId.Papyrus.Host') && existingProcessIds.indexOf(p.pid) === -1
            )!.pid;

            if (serverProcessId) {
                exec(`vsjitdebugger.exe -p ${serverProcessId}`);
            }
        }
    }

    public async stop() {
        if (!this._client) {
            return;
        }

        try {
            this._fsWatcher.dispose();
            await this._client.stop();
        } finally {
            this._fsWatcher = null;
            this._client = null;
        }
    }
}
