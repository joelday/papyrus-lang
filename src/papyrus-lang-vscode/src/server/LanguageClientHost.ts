import * as path from 'path';
import * as fs from 'fs';
import { Disposable, ExtensionContext } from 'vscode';
import { promisify } from 'util';
import winreg from 'winreg';
const exists = promisify(fs.exists);

import { LanguageClient, ILanguageClient } from './LanguageClient';
import { PapyrusGame, getShortDisplayNameForGame } from '../PapyrusGame';
import { IGameConfig } from '../ExtensionConfigProvider';
import { Observable, BehaviorSubject } from 'rxjs';

// flatMap(async (gameConfig) => {
//     if (!gameConfig.enabled) {
//         return { gameConfig };
//     }

//     const installPath = await resolveInstallPathWithRegistryFallback(game, gameConfig.installPath);

//     const compilerAssemblyPath = getCompilerAssemblyPath(installPath);
//     const compilerExists = await exists(compilerAssemblyPath);

//     if (!compilerExists) {
//         return { gameConfig };
//     }

//     return { gameConfig, compilerAssemblyPath };
// }),
// asyncDisposable(
//     async (setupInfo) => {
//         const { gameConfig, compilerAssemblyPath } = setupInfo;

//         if (!gameConfig.enabled) {
//             return { game, client: null, error: null, status: ClientHostStatus.disabled };
//         }

//         const client = new LanguageClient({
//             game,
//             toolPath: this._context.asAbsolutePath(getToolPath(game)),
//             serviceDisplayName: `Papyrus Language Service (${getDisplayNameForGame(game)})`,
//             toolArguments: {
//                 compilerAssemblyPath,
//                 creationKitInstallPath: gameConfig.installPath,
//                 relativeIniPaths: gameConfig.creationKitIniFiles,
//                 flagsFileName: getFlagsFileName(game),
//                 ambientProjectName: `${getDisplayNameForGame(game)} CreationKit`,
//                 ...getDefaultImports(game),
//             },
//         });

//         await client.start();

//         return client;
//     },
//     (host) => host.start(),
//     fastDeepEqual
// ),

export enum ClientHostStatus {
    none,
    disabled,
    starting,
    running,
    error,
    missing,
}

export interface ILanguageClientHost {
    readonly game: PapyrusGame;
    readonly status: Observable<ClientHostStatus>;
    readonly client: ILanguageClient;
    readonly error: Observable<string>;
}

export class LanguageClientHost implements ILanguageClientHost, Disposable {
    private readonly _game: PapyrusGame;
    private readonly _config: IGameConfig;
    private readonly _context: ExtensionContext;
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

    async start() {
        if (!this._config.enabled) {
            this._status.next(ClientHostStatus.disabled);
            return;
        }

        if (this._status.value !== ClientHostStatus.none) {
            return;
        }

        try {
            const installPath = await resolveInstallPathWithRegistryFallback(this._game, this._config.installPath);

            const compilerAssemblyPath = installPath && getCompilerAssemblyPath(installPath);
            const compilerExists = compilerAssemblyPath && (await exists(compilerAssemblyPath));

            if (!compilerExists) {
                this._status.next(ClientHostStatus.missing);
                return;
            }

            this._client = new LanguageClient({
                game: this._game,
                toolPath: this._context.asAbsolutePath(getToolPath(this._game)),
                serviceDisplayName: `Papyrus (${getShortDisplayNameForGame(this._game)})`,
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
            this._error.next(error.toString());
            this._status.next(ClientHostStatus.error);
        }
    }

    dispose() {
        if (this._client) {
            this._client.dispose();
        }
    }
}

function getCompilerAssemblyPath(installPath: string) {
    return path.join(installPath, 'Papyrus Compiler');
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

function getRegistryKeyForGame(game: PapyrusGame) {
    switch (game) {
        case PapyrusGame.fallout4:
            return 'Fallout4';
        case PapyrusGame.skyrim:
            return 'Skyrim';
        case PapyrusGame.skyrimSpecialEdition:
            return 'Skyrim Special Edition';
    }
}

async function resolveInstallPathWithRegistryFallback(game: PapyrusGame, installPath: string) {
    if (await exists(installPath)) {
        return installPath;
    }

    const reg = new winreg({
        key: `\\SOFTWARE\\${process.arch === 'x64' ? 'WOW6432Node\\' : ''}Bethesda Softworks\\${getRegistryKeyForGame(
            game
        )}`,
    });

    try {
        const item = await promisify(reg.get).call(reg, 'installed path');

        if (await exists(item.value)) {
            return item.value;
        }
    } catch (_) {}

    return null;
}
