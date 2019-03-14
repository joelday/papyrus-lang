import { Disposable, ExtensionContext, OutputChannel, window } from 'vscode';

import { LanguageClient, ILanguageClient } from './LanguageClient';
import { PapyrusGame, getShortDisplayNameForGame } from '../PapyrusGame';
import { IGameConfig } from '../ExtensionConfigProvider';
import { Observable, BehaviorSubject } from 'rxjs';
import { getInstallInfo } from '../Utilities';

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
}

export class LanguageClientHost implements ILanguageClientHost, Disposable {
    private readonly _game: PapyrusGame;
    private readonly _config: IGameConfig;
    private readonly _context: ExtensionContext;
    private _outputChannel: OutputChannel;
    private _client: LanguageClient;

    private readonly _status = new BehaviorSubject(ClientHostStatus.none);
    private readonly _error = new BehaviorSubject(null);

    constructor(game: PapyrusGame, config: IGameConfig, context: ExtensionContext) {
        this._game = game;
        this._config = config;
        this._context = context;
    }

    get game() {
        return this._game;
    }

    get status() {
        return this._status.asObservable();
    }

    get error() {
        return this._error.asObservable();
    }

    get client() {
        return this._client;
    }

    get outputChannel() {
        return this._outputChannel;
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
            const { installPath, compilerAssemblyPath } = await getInstallInfo(
                this._game,
                this._config.installPath,
                this._config.compilerPath
            );

            if (!installPath) {
                this._status.next(ClientHostStatus.missing);
                return;
            }

            if (!compilerAssemblyPath) {
                this._status.next(ClientHostStatus.compilerMissing);
                return;
            }

            this._client = new LanguageClient({
                game: this._game,
                toolPath: this._context.asAbsolutePath(getToolPath(this._game)),
                outputChannel: this._outputChannel,
                toolArguments: {
                    compilerAssemblyPath,
                    creationKitInstallPath: this._config.installPath,
                    relativeIniPaths: this._config.creationKitIniFiles,
                    flagsFileName: getFlagsFileName(this._game),
                    ambientProjectName: `${getShortDisplayNameForGame(this._game)} Creation Kit`,
                    ...getDefaultImports(this._game),
                },
            });

            this._status.next(ClientHostStatus.starting);
            await this._client.start();
            this._status.next(ClientHostStatus.running);
        } catch (error) {
            this._outputChannel.appendLine(`Error on language service pre-start: ${error.toString()}`);

            this._error.next(error.toString());
            this._status.next(ClientHostStatus.error);
        }
    }

    dispose() {
        if (this._outputChannel) {
            this._outputChannel.dispose();
        }

        if (this._client) {
            this._client.dispose();
        }
    }
}

function getToolGameName(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim';
    }
}

function getToolPath(game: PapyrusGame) {
    const toolGameName = getToolGameName(game);
    return `./bin/Debug/net461/DarkId.Papyrus.Host.${toolGameName}/DarkId.Papyrus.Host.${toolGameName}.exe`;
}

function getFlagsFileName(game: PapyrusGame) {
    return game === PapyrusGame.fallout4 ? 'Institute_Papyrus_Flags.flg' : 'TESV_Papyrus_Flags.flg';
}

function getDefaultImports(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return {
                defaultScriptSourceFolder: '.\\Data\\Scripts\\Source\\User\\',
                defaultAdditionalImports: '$(source);.\\Data\\Scripts\\Source\\Base\\',
            };
        case PapyrusGame.skyrim:
            return {
                defaultScriptSourceFolder: '.\\Data\\Scripts\\Source\\',
            };
        case PapyrusGame.skyrimSpecialEdition:
            return {
                defaultScriptSourceFolder: '.\\Data\\Source\\Scripts\\',
            };
    }
}
