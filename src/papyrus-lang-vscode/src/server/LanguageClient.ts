import { LanguageClient as BaseClient, TextDocumentIdentifier } from 'vscode-languageclient';
import { workspace, FileSystemWatcher, OutputChannel, TextDocument } from 'vscode';

import { DocumentScriptInfo, documentScriptInfoRequestType } from './messages/DocumentScriptInfo';
import { DocumentSyntaxTree, documentSyntaxTreeRequestType } from './messages/DocumentSyntaxTree';
import { PapyrusGame } from '../PapyrusGame';

function toCommandLineArgs(obj: Object): string[] {
    return [].concat(
        ...Object.keys(obj).map((key) => {
            const value = obj[key];

            if (typeof value === 'undefined' || value === null) {
                return [];
            }

            return [
                `--${key}`,
                ...(Array.isArray(value) ? value.map((element) => element.toString()) : [value.toString()]),
            ];
        })
    );
}

export interface ILanguageClientOptions {
    game: PapyrusGame;
    outputChannel: OutputChannel;
    toolPath: string;
    toolArguments: IToolArguments;
}

export interface IToolArguments {
    compilerAssemblyPath: string;
    flagsFileName: string;
    ambientProjectName: string;
    defaultScriptSourceFolder?: string;
    defaultAdditionalImports?: string;
    creationKitInstallPath: string;
    relativeIniPaths: string[];
}

export interface ILanguageClient {
    requestScriptInfo(uri: string): Thenable<DocumentScriptInfo>;
    requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree>;
}

export class LanguageClient implements ILanguageClient {
    private readonly _client: BaseClient;
    private readonly _fsWatcher: FileSystemWatcher;
    private readonly _outputChannel: OutputChannel;
    private _isDisposed: boolean;

    constructor(options: ILanguageClientOptions) {
        this._fsWatcher = workspace.createFileSystemWatcher('**/*.{flg,ppj,psc}');
        this._client = new BaseClient(
            options.game.toString(),
            {
                command: options.toolPath,
                args: toCommandLineArgs(options.toolArguments),
            },
            {
                outputChannel: options.outputChannel,
                documentSelector: [
                    { scheme: 'file', language: 'papyrus' },
                    { scheme: 'file', language: 'papyrus-project' },
                ],
                synchronize: {
                    fileEvents: this._fsWatcher,
                    configurationSection: 'papyrus',
                },
            }
        );

        this._outputChannel = options.outputChannel;
    }

    async start() {
        if (this._client.needsStart()) {
            this._outputChannel.appendLine('Starting language service...');

            this._client.start();
            await this._client.onReady();

            this._outputChannel.appendLine('Language service started.');

            if (this._isDisposed) {
                await this._client.stop();
            }
        }
    }

    async stop() {
        if (this._client.needsStop()) {
            this._outputChannel.appendLine('Stopping language service...');

            await this._client.stop();

            this._outputChannel.appendLine('Language service stopped.');
        }
    }

    requestScriptInfo(uri: string): Thenable<DocumentScriptInfo> {
        return this._client.sendRequest(documentScriptInfoRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree> {
        return this._client.sendRequest(documentSyntaxTreeRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    dispose() {
        if (this._isDisposed) {
            return;
        }

        this._outputChannel.appendLine('Disposing language client.');

        this._isDisposed = true;

        this.stop();
        this._fsWatcher.dispose();
    }
}
