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
    if (process.platform !== 'win32') {
        const args = [toolPath, '--compilerAssemblyPath', compilerAssemblyPath];

        if (process.env['PAPYRUS_EXTENSION_DEBUG']) {
            args.unshift('--debug', '--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:2077');
        }

        return {
            command: 'mono',
            args,
        };
    } else {
        return { command: toolPath, args: ['--compilerAssemblyPath', compilerAssemblyPath] };
    }
}

export interface DocumentSyntaxTreeParams {
    textDocument: TextDocumentIdentifier;
}

export interface DocumentSyntaxTree {
    root: DocumentSyntaxTreeNode;
    pCompilerRoot: DocumentSyntaxTreeNode;
}

export interface DocumentSyntaxTreeNode {
    name: string;
    kind: string;
    children: DocumentSyntaxTreeNode[];
    range: Range;
}

export interface ProvideDocumentSyntaxTreeSignature {
    (document: TextDocument, token: CancellationToken): ProviderResult<DocumentSyntaxTree>;
}

export const documentRequestType = {
    type: new RequestType<DocumentSyntaxTreeParams, DocumentSyntaxTree | null, void, TextDocumentRegistrationOptions>(
        'textDocument/syntaxTree'
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

    public requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree> {
        return this._client.sendRequest(documentRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    public async start() {
        if (this._client) {
            return;
        }

        /*'--debug', '--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:2077', */

        this._fsWatcher = workspace.createFileSystemWatcher('**/*.{flg,ppj,psc}');

        const clientOptions: LanguageClientOptions = {
            documentSelector: [
                { scheme: 'file', language: 'papyrus' },
                { scheme: 'file', language: 'papyrus-project' },
            ],
            synchronize: {
                fileEvents: this._fsWatcher,
            },
        };

        // Create the language client and start the client.
        this._client = new LanguageClient('papyrus', 'Papyrus Language Service', this._serverOptions, clientOptions);

        this._client.trace = Trace.Verbose;
        this._client.start();

        await this._client.onReady();

        if (process.platform === 'win32' && process.env['PAPYRUS_EXTENSION_DEBUG']) {
            const processes = await psList();
            const serverProcessId = processes.find((p) => p.name === 'DarkId.Papyrus.Host.exe')!.pid;

            if (serverProcessId) {
                exec(`vsjitdebugger.exe -p ${serverProcessId}`);
            }
        }
    }

    public async restart() {
        try {
            await this.stop();
        } finally {
            await this.start();
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