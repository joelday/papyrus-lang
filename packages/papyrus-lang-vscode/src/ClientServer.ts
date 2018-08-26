import { Disposable, FileSystemWatcher, workspace } from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} from 'vscode-languageclient';

export class ClientServer {
    private readonly _serverOptions: ServerOptions;
    private _client: LanguageClient;
    private _fsWatcher: FileSystemWatcher;

    constructor(serverModulePath: string) {
        this._serverOptions = {
            run: { module: serverModulePath, transport: TransportKind.ipc },
            debug: {
                module: serverModulePath,
                transport: TransportKind.ipc,
                options: { execArgv: ['--nolazy', '--inspect=6009'] },
            },
        };
    }

    public async start() {
        if (this._client) {
            return;
        }

        this._fsWatcher = workspace.createFileSystemWatcher(
            '**/*.{flg,ppj,psc}'
        );

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
        this._client = new LanguageClient(
            'papyrus',
            'Papyrus Language Service',
            this._serverOptions,
            clientOptions
        );

        this._client.start();

        await this._client.onReady();
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
