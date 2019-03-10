import { createDecorator } from 'decoration-ioc';
import { LanguageClient } from './LanguageClient';
import { PapyrusGame, getDisplayNameForGame } from '../PapyrusGame';
import { Observable, Subscription, from } from 'rxjs';
import { IExtensionConfigProvider } from '../ExtensionConfigProvider';
import { flatMap, map, catchError } from 'rxjs/operators';
import * as path from 'path';
import * as fs from 'fs';
import { asyncDisposable } from '../common/Reactive';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, Disposable, DiagnosticSeverity, workspace } from 'vscode';
import { promisify } from 'util';
import fastDeepEqual from 'fast-deep-equal';
import winreg from 'winreg';

const exists = promisify(fs.exists);

export interface ILanguageClientHost {
    readonly client: LanguageClient;
    readonly error: any;
}

export interface ILanguageClientManager {
    readonly clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;
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

    const item = await promisify(reg.get).call(reg, 'installed path');

    if (await exists(item.value)) {
        return item.value;
    }

    return null;
}

export class LanguageClientManager implements Disposable, ILanguageClientManager {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _context: ExtensionContext;
    private readonly _clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;

    private readonly _clientSubscriptions: Subscription[];

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._configProvider = configProvider;
        this._context = context;

        const createClientObservable = (game: PapyrusGame) => {
            return this._configProvider.config.pipe(
                map((config) => config[game]),
                flatMap(async (gameConfig) => {
                    const installPath = await resolveInstallPathWithRegistryFallback(game, gameConfig.installPath);

                    const compilerAssemblyPath = getCompilerAssemblyPath(installPath);
                    const compilerExists = await exists(compilerAssemblyPath);

                    if (!compilerExists) {
                        throw new Error(
                            `${getDisplayNameForGame(game)} compiler assembly path '${compilerAssemblyPath}' not found.`
                        );
                    }

                    return { gameConfig, compilerAssemblyPath };
                }),
                asyncDisposable(async ({ gameConfig, compilerAssemblyPath }) => {
                    const client = new LanguageClient({
                        game,
                        toolPath: this._context.asAbsolutePath(getToolPath(game)),
                        serviceDisplayName: `Papyrus Language Service (${getDisplayNameForGame(game)})`,
                        toolArguments: {
                            compilerAssemblyPath,
                            creationKitInstallPath: gameConfig.installPath,
                            relativeIniPaths: gameConfig.creationKitIniFiles,
                            flagsFileName: getFlagsFileName(game),
                            ambientProjectName: `${getDisplayNameForGame(game)} CreationKit`,
                            ...getDefaultImports(game),
                        },
                    });

                    await client.start();

                    return client;
                }, fastDeepEqual),
                map((client) => ({ client, error: null })),
                catchError((error, _) => new Observable((s) => s.next({ error, client: null })))
            ) as Observable<ILanguageClientHost>;
        };

        this._clients = new Map([
            [PapyrusGame.fallout4, createClientObservable(PapyrusGame.fallout4)],
            [PapyrusGame.skyrim, createClientObservable(PapyrusGame.skyrim)],
            [PapyrusGame.skyrimSpecialEdition, createClientObservable(PapyrusGame.skyrimSpecialEdition)],
        ]);

        this._clientSubscriptions = Array.from(this._clients).map((client) =>
            client[1].subscribe((instance) => {
                console.log('Client manager instance:', client[0], instance);
            })
        );
    }

    get clients() {
        return this._clients;
    }

    dispose() {
        for (const subscription of this._clientSubscriptions) {
            subscription.unsubscribe();
        }
    }
}

export const ILanguageClientManager = createDecorator<ILanguageClientManager>('languageClientManager');
