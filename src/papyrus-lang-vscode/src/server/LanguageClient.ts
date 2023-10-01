import { LanguageClient as BaseClient, TextDocumentIdentifier, NotificationType } from 'vscode-languageclient';
import { workspace, FileSystemWatcher, OutputChannel } from 'vscode';

import { DocumentScriptInfo, documentScriptInfoRequestType } from './messages/DocumentScriptInfo';
import { DocumentSyntaxTree, documentSyntaxTreeRequestType } from './messages/DocumentSyntaxTree';
import { PapyrusGame } from "../PapyrusGame";
import { toCommandLineArgs } from '../Utilities';
import { ProjectInfos, projectInfosRequestType } from './messages/ProjectInfos';
import { Observable, BehaviorSubject } from 'rxjs';
import { DocumentAssembly, documentAssemblyRequestType } from './messages/DocumentAssembly';

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
    readonly projectsUpdated: Observable<void>;

    requestProjectInfos(): Thenable<ProjectInfos | null>;
    requestScriptInfo(uri: string): Thenable<DocumentScriptInfo | null>;
    requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree | null>;
    requestAssembly(uri: string): Thenable<DocumentAssembly | null>;
}

const projectsUpdatedNotificationType = {
    type: new NotificationType<void, void>('papyrus/projectsUpdated'),
};

export class LanguageClient implements ILanguageClient {
    private readonly _client: BaseClient;
    private readonly _fsWatcher: FileSystemWatcher;
    private readonly _outputChannel: OutputChannel;
    private _isDisposed: boolean = false;

    private readonly _projectsUpdated = new BehaviorSubject<void>(undefined);

    get projectsUpdated(): Observable<void> {
        return this._projectsUpdated;
    }

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

            this._client.onNotification(projectsUpdatedNotificationType.type, () => {
                this._projectsUpdated.next(undefined);
            });

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

    requestProjectInfos(): Thenable<ProjectInfos | null> {
        try {
            return this._client.sendRequest(projectInfosRequestType.type, {});
        } catch (_) {
            return Promise.resolve({
                projects: [],
            });
        }
    }

    requestAssembly(uri: string): Thenable<DocumentAssembly | null> {
        return this._client.sendRequest(documentAssemblyRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    requestScriptInfo(uri: string): Thenable<DocumentScriptInfo | null> {
        return this._client.sendRequest(documentScriptInfoRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    requestSyntaxTree(uri: string): Thenable<DocumentSyntaxTree | null> {
        return this._client.sendRequest(documentSyntaxTreeRequestType.type, {
            textDocument: TextDocumentIdentifier.create(uri),
        });
    }

    dispose() {
        if (this._isDisposed) {
            return;
        }

        this._projectsUpdated.complete();

        this._outputChannel.appendLine('Disposing language client.');

        this._isDisposed = true;

        this.stop();
        this._fsWatcher.dispose();
    }
}
