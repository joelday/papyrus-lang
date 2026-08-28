import { OutputChannel } from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';
import { IManagedLanguageClient } from './PapyrusLanguageClientManager';

/**
 * Thin adapter around `vscode-languageclient`'s `LanguageClient` satisfying
 * the narrow `IManagedLanguageClient` interface that
 * `PapyrusLanguageClientManager` depends on, so the manager's tests never
 * need to construct a real client.
 */
export class RealLanguageClient implements IManagedLanguageClient {
    private readonly _client: LanguageClient;

    constructor(serverPath: string, outputChannel: OutputChannel) {
        const serverOptions: ServerOptions = {
            command: serverPath,
            transport: TransportKind.stdio,
        };

        const clientOptions: LanguageClientOptions = {
            documentSelector: [
                { scheme: 'file', language: 'papyrus' },
                { scheme: 'file', language: 'papyrus-project' },
            ],
            outputChannel,
        };

        this._client = new LanguageClient('papyrus', 'Papyrus Language Server', serverOptions, clientOptions);
    }

    async start(): Promise<void> {
        await this._client.start();
    }

    async stop(): Promise<void> {
        if (this._client.needsStop()) {
            await this._client.stop();
        }
    }

    dispose() {
        void this.stop();
    }
}
