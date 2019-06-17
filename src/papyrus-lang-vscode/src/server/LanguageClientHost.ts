import { Disposable, ExtensionContext, OutputChannel, window, TextDocument } from 'vscode';

import { LanguageClient, ILanguageClient, IToolArguments } from './LanguageClient';
import { PapyrusGame, getShortDisplayNameForGame } from '../PapyrusGame';
import { IGameConfig } from '../ExtensionConfigProvider';
import { Observable, BehaviorSubject } from 'rxjs';
import { ICreationKitInfo } from '../CreationKitInfoProvider';
import { DocumentScriptInfo } from './messages/DocumentScriptInfo';
import { shareReplay, take, map, switchMap } from 'rxjs/operators';
import { getDefaultFlagsFileNameForGame, getLanguageToolPath } from '../Utilities';
import { ProjectInfos } from './messages/ProjectInfos';

export enum ClientHostStatus {
    none,
    disabled,
    starting,
    running,
    error,
    missing,
    compilerMissing,
}

export interface ILanguageClientHost {
    readonly game: PapyrusGame;
    readonly status: Observable<ClientHostStatus>;
    readonly client: ILanguageClient;
    readonly error: Observable<string>;
    readonly outputChannel: OutputChannel;
    readonly projectInfos: Observable<ProjectInfos>;
    getDocumentScriptStatus(document: TextDocument): Promise<IScriptDocumentStatus>;
}

export interface IScriptDocumentStatus {
    readonly game: PapyrusGame;
    readonly documentIsUnresolved: boolean;
    readonly documentIsOverridden: boolean;
    readonly scriptInfo: DocumentScriptInfo;
}

export class LanguageClientHost implements ILanguageClientHost, Disposable {
    private readonly _game: PapyrusGame;
    private readonly _config: IGameConfig;
    private readonly _creationKitInfo: ICreationKitInfo;
    private readonly _context: ExtensionContext;
    private _outputChannel: OutputChannel;
    private _client: LanguageClient;

    private _projectInfos: Observable<ProjectInfos>;

    private readonly _status = new BehaviorSubject(ClientHostStatus.none);
    private readonly _error = new BehaviorSubject(null);

    constructor(game: PapyrusGame, config: IGameConfig, creationKitInfo: ICreationKitInfo, context: ExtensionContext) {
        this._game = game;
        this._config = config;
        this._creationKitInfo = creationKitInfo;
        this._context = context;

        this._projectInfos = this._status.pipe(
            switchMap((status) => {
                if (status !== ClientHostStatus.running) {
                    return Promise.resolve(null);
                }

                return this._client.projectsUpdated;
            }),
            switchMap(() => (this._client ? this._client.requestProjectInfos() : Promise.resolve(null)))
        );
    }

    get game() {
        return this._game;
    }

    get status() {
        return this._status.asObservable().pipe(shareReplay(1));
    }

    get error() {
        return this._error.asObservable().pipe(shareReplay(1));
    }

    get client() {
        return this._client;
    }

    get outputChannel() {
        return this._outputChannel;
    }

    get projectInfos() {
        return this._projectInfos;
    }

    async start() {
        if (!this._config.enabled) {
            this._status.next(ClientHostStatus.disabled);
            return;
        }

        if (!this._outputChannel) {
            this._outputChannel = window.createOutputChannel(`Papyrus (${getShortDisplayNameForGame(this._game)})`);
        }

        if (this._status.value !== ClientHostStatus.none) {
            return;
        }

        try {
            if (!this._creationKitInfo.resolvedInstallPath) {
                this._status.next(ClientHostStatus.missing);
                return;
            }

            if (!this._creationKitInfo.resolvedCompilerPath) {
                this._status.next(ClientHostStatus.compilerMissing);
                return;
            }

            const toolArguments: IToolArguments = {
                compilerAssemblyPath: this._creationKitInfo.resolvedCompilerPath,
                creationKitInstallPath: this._creationKitInfo.resolvedInstallPath,
                relativeIniPaths: this._config.creationKitIniFiles,
                flagsFileName: getDefaultFlagsFileNameForGame(this._game),
                ambientProjectName: `${getShortDisplayNameForGame(this._game)} Creation Kit`,
                defaultScriptSourceFolder: this._creationKitInfo.config.Papyrus.sScriptSourceFolder,
                defaultAdditionalImports: this._creationKitInfo.config.Papyrus.sAdditionalImports,
            };

            this._outputChannel.appendLine(`Creating Language Client instance with options:`);
            this._outputChannel.appendLine(JSON.stringify(toolArguments, null, 4));
            this._outputChannel.appendLine('');

            this._client = new LanguageClient({
                game: this._game,
                toolPath: this._context.asAbsolutePath(getLanguageToolPath(this._game)),
                outputChannel: this._outputChannel,
                toolArguments,
            });

            this._status.next(ClientHostStatus.starting);
            await this._client.start();
            this._status.next(ClientHostStatus.running);
        } catch (error) {
            this._outputChannel.appendLine(`Error on language service pre-start: ${error.toString()}`);
            if (error instanceof Error) {
                this._outputChannel.appendLine(error.stack);
            }

            this._error.next(error.toString());
            this._status.next(ClientHostStatus.error);
        }
    }

    async getDocumentScriptStatus(document: TextDocument): Promise<IScriptDocumentStatus> {
        if ((await this._status.pipe(take(1)).toPromise()) !== ClientHostStatus.running) {
            return null;
        }

        const scriptInfo = await this._client.requestScriptInfo(document.uri.toString());

        const documentIsUnresolved = scriptInfo.identifiers.length === 0;
        const documentIsOverridden =
            !documentIsUnresolved &&
            !scriptInfo.identifierFiles.some((identifierFile) =>
                identifierFile.files.some((file) => file.toLowerCase() === document.uri.fsPath.toLowerCase())
            );

        return {
            game: this._game,
            documentIsUnresolved,
            documentIsOverridden,
            scriptInfo,
        };
    }

    dispose() {
        this._status.complete();
        this._error.complete();

        if (this._outputChannel) {
            this._outputChannel.dispose();
        }

        if (this._client) {
            this._client.dispose();
        }
    }
}
