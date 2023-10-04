import { Disposable, OutputChannel, window, TextDocument } from 'vscode';

import { LanguageClient, ILanguageClient, IToolArguments } from './LanguageClient';
import { PapyrusGame, getShortDisplayNameForGame, getDefaultFlagsFileNameForGame } from '../PapyrusGame';
import { IGameConfig } from '../ExtensionConfigProvider';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { ICreationKitInfo } from '../CreationKitInfoProvider';
import { DocumentScriptInfo } from './messages/DocumentScriptInfo';
import { shareReplay, take, switchMap } from 'rxjs/operators';
import { IPathResolver } from '../common/PathResolver';
import { ProjectInfos } from './messages/ProjectInfos';
import { inject } from 'inversify';

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
    readonly client: ILanguageClient | null;
    readonly error: Observable<string | null>;
    readonly outputChannel: OutputChannel | null;
    readonly projectInfos: Observable<ProjectInfos | null>;
    getDocumentScriptStatus(document: TextDocument): Promise<IScriptDocumentStatus | null>;
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
    private readonly _pathResolver: IPathResolver;
    private _outputChannel: OutputChannel | null = null;
    private _client: LanguageClient | null = null;

    private _projectInfos: Observable<ProjectInfos | null>;

    private readonly _status = new BehaviorSubject(ClientHostStatus.none);
    private readonly _error = new BehaviorSubject<string | null>(null);

    constructor(
        game: PapyrusGame,
        config: IGameConfig,
        creationKitInfo: ICreationKitInfo,
        @inject(IPathResolver) pathResolver: IPathResolver
    ) {
        this._game = game;
        this._config = config;
        this._creationKitInfo = creationKitInfo;
        this._pathResolver = pathResolver;

        this._projectInfos = this._status.pipe(
            switchMap((status) => {
                if (status !== ClientHostStatus.running) {
                    return of(null);
                }

                // Since we're in ClientHostStatus.running, then this._client is defined.
                return this._client!.projectsUpdated;
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
            const defaultFlags = getDefaultFlagsFileNameForGame(this._game);
            const toolArguments: IToolArguments = {
                compilerAssemblyPath: this._creationKitInfo.resolvedCompilerPath,
                creationKitInstallPath: this._creationKitInfo.resolvedInstallPath,
                relativeIniPaths: this._config.creationKitIniFiles,
                flagsFileName: defaultFlags,
                ambientProjectName: 'Creation Kit',
                defaultScriptSourceFolder: this._creationKitInfo.config.Papyrus?.sScriptSourceFolder,
                defaultAdditionalImports: this._creationKitInfo.config.Papyrus?.sAdditionalImports,
            };

            this._outputChannel.appendLine(`Creating Language Client instance with options:`);
            this._outputChannel.appendLine(JSON.stringify(toolArguments, null, 4));
            this._outputChannel.appendLine('');

            this._client = new LanguageClient({
                game: this._game,
                toolPath: await this._pathResolver.getLanguageToolPath(this._game),
                outputChannel: this._outputChannel,
                toolArguments,
            });

            this._status.next(ClientHostStatus.starting);
            await this._client.start();
            this._status.next(ClientHostStatus.running);
        } catch (err) {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const error = err instanceof Error ? err : new Error((err as any).toString());

            this._outputChannel.appendLine(`Error on language service pre-start: ${error.toString()}`);
            if (error.stack) {
                this._outputChannel.appendLine(error.stack);
            }

            this._error.next(error.toString());
            this._status.next(ClientHostStatus.error);
        }
    }

    async getDocumentScriptStatus(document: TextDocument): Promise<IScriptDocumentStatus | null> {
        if (!this._client) {
            return null;
        }

        if ((await this._status.pipe(take(1)).toPromise()) !== ClientHostStatus.running) {
            return null;
        }

        const scriptInfo = await this._client.requestScriptInfo(document.uri.toString());

        if (!scriptInfo) {
            return null;
        }

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
