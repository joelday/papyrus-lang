import { LanguageClient as BaseClient, TextDocumentIdentifier } from 'vscode-languageclient';
import { workspace, FileSystemWatcher } from 'vscode';

import { DocumentScriptInfo, documentScriptInfoRequestType } from './messages/DocumentScriptInfo';
import { DocumentSyntaxTree, documentSyntaxTreeRequestType } from './messages/DocumentSyntaxTree';
import { PapyrusGame } from '../PapyrusGame';

function toCommandLineArgs(obj: Object): string[] {
    return [].concat(
        ...Object.keys(obj).map((key) => {
            const value = obj[key];

            return [
                `--${key}`,
                ...(Array.isArray(value) ? value.map((element) => element.toString()) : [value.toString()]),
            ];
        })
    );
}

export interface ILanguageClientOptions {
    game: PapyrusGame;
    serviceDisplayName: string;
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

export class LanguageClient {
    private readonly _client: BaseClient;
    private readonly _fsWatcher: FileSystemWatcher;

    constructor(options: ILanguageClientOptions) {
        this._fsWatcher = workspace.createFileSystemWatcher('**/*.{flg,ppj,psc}');
        this._client = new BaseClient(
            options.game.toString(),
            options.serviceDisplayName,
            {
                command: options.toolPath,
                args: toCommandLineArgs(options.toolArguments),
            },
            {
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
    }

    async start() {
        if (this._client.needsStart()) {
            console.log('Starting language service...');

            this._client.start();
            await this._client.onReady();

            console.log('Language service started.');
        }
    }

    async stop() {
        if (this._client.needsStop()) {
            console.log('Stopping language service...');

            await this._client.stop();

            console.log('Language service stopped.');
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
        console.log('Disposing language client.');

        this.stop();
        this._fsWatcher.dispose();
    }
}
