import { createDecorator } from 'decoration-ioc';
import { PapyrusGame, getGames } from './PapyrusGame';
import { IExtensionConfigProvider } from './ExtensionConfigProvider';
import { Observable, combineLatest } from 'rxjs';
import { map, mergeMap, shareReplay } from 'rxjs/operators';
import { resolveInstallPath, inDevelopmentEnvironment, getDevelopmentCompilerFolderForGame } from './Utilities';
import * as path from 'path';
import * as ini from 'ini';
import { all as deepMergeAll } from 'deepmerge';
import * as fs from 'fs';
import { promisify } from 'util';
import { IExtensionContext } from './common/vscode/IocDecorators';
import { ExtensionContext } from 'vscode';

const readFile = promisify(fs.readFile);
const exists = promisify(fs.exists);

export interface ICreationKitInfo {
    resolvedInstallPath: string;
    resolvedCompilerPath: string;
    config: ICreationKitConfig;
}

export interface ICreationKitConfig {
    Papyrus?: ICreationKitPapyrusConfig;
}

export interface ICreationKitPapyrusConfig {
    sScriptSourceFolder?: string;
    sAdditionalImports?: string;
    sScriptCompiledFolder?: string;
    sCompilerFolder?: string;
}

function getDefaultPapyrusConfigForGame(game: PapyrusGame): ICreationKitPapyrusConfig {
    const sScriptCompiledFolder = 'Data/Scripts/';
    const sCompilerFolder = 'Papyrus Compiler/';

    switch (game) {
        case PapyrusGame.fallout4:
            return {
                sScriptSourceFolder: '.\\Data\\Scripts\\Source\\User\\',
                sAdditionalImports: '$(source);.\\Data\\Scripts\\Source\\Base\\',
                sCompilerFolder,
                sScriptCompiledFolder,
            };
        case PapyrusGame.skyrim:
            return {
                sScriptSourceFolder: '.\\Data\\Scripts\\Source\\',
                sCompilerFolder,
                sScriptCompiledFolder,
            };
        case PapyrusGame.skyrimSpecialEdition:
            return {
                sScriptSourceFolder: '.\\Data\\Source\\Scripts\\',
                sCompilerFolder,
                sScriptCompiledFolder,
            };
    }
}

function getDefaultConfigForGame(game: PapyrusGame): ICreationKitConfig {
    return {
        Papyrus: getDefaultPapyrusConfigForGame(game),
    };
}

export interface ICreationKitInfoProvider {
    readonly infos: ReadonlyMap<PapyrusGame, Observable<ICreationKitInfo>>;
}

export class CreationKitInfoProvider {
    private readonly _infos: Map<PapyrusGame, Observable<ICreationKitInfo>>;

    constructor(@IExtensionConfigProvider infoProvider: IExtensionConfigProvider, @IExtensionContext extensionContext: ExtensionContext) {
        const createInfoObservable = (game: PapyrusGame) => {
            const gameConfig = infoProvider.config.pipe(map((config) => config[game]));

            const resolvedInstallPath = gameConfig.pipe(
                mergeMap((gameConfig) => resolveInstallPath(game, gameConfig.installPath, extensionContext)),
                shareReplay(1)
            );

            const configAndResolvedInstallPath = combineLatest(gameConfig, resolvedInstallPath);

            const iniFilePaths = configAndResolvedInstallPath.pipe(
                mergeMap(async ([gameConfig, resolvedInstallPath]) => {
                    if (resolvedInstallPath === null) {
                        return { iniFilePaths: [] as string[] };
                    }

                    return {
                        iniFilePaths: gameConfig.creationKitIniFiles.map((iniFile) =>
                            path.resolve(resolvedInstallPath, iniFile)
                        ),
                    };
                })
            );

            const iniTexts = iniFilePaths.pipe(
                mergeMap((paths) =>
                    Promise.all(
                        paths.iniFilePaths.map(async (iniFilePath) =>
                            (await exists(iniFilePath)) ? readFile(iniFilePath, 'utf-8') : Promise.resolve(null)
                        )
                    )
                )
            );

            const parsedInis = iniTexts.pipe(
                map((iniTexts) => iniTexts.filter((iniText) => iniText !== null).map((iniText) => ini.parse(iniText)))
            );

            const mergedIni = parsedInis.pipe(
                map((iniObjects) => deepMergeAll([getDefaultConfigForGame(game), ...iniObjects]) as ICreationKitConfig)
            );

            return combineLatest(resolvedInstallPath, mergedIni).pipe(
                mergeMap(
                    async ([resolvedInstallPath, mergedIni]) => {
                        const compilerPath = path.resolve(resolvedInstallPath, mergedIni.Papyrus.sCompilerFolder);
                        const resolvedCompilerPath = await exists(compilerPath) ? compilerPath : inDevelopmentEnvironment() ?
                            path.resolve(resolvedInstallPath, getDevelopmentCompilerFolderForGame(game)) : null;

                        return {
                            resolvedInstallPath,
                            resolvedCompilerPath: inDevelopmentEnvironment() && !(await exists(resolvedCompilerPath)) ? null : resolvedCompilerPath,
                            config: mergedIni,
                        } as ICreationKitInfo;
                    }
                ),
                shareReplay(1)
            );
        };

        this._infos = new Map([
            ...getGames().map(
                (game) => [game, createInfoObservable(game)] as [PapyrusGame, Observable<ICreationKitInfo>]
            ),
        ]);
    }

    get infos() {
        return this._infos;
    }
}

export const ICreationKitInfoProvider = createDecorator<ICreationKitInfoProvider>('creationKitInfoProvider');
