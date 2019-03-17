import { createDecorator } from 'decoration-ioc';
import { LanguageClient } from './LanguageClient';
import { PapyrusGame, getGames } from '../PapyrusGame';
import { Observable, Subscription, concat, combineLatest } from 'rxjs';
import { IExtensionConfigProvider, IGameConfig } from '../ExtensionConfigProvider';
import { map, share, shareReplay } from 'rxjs/operators';
import { asyncDisposable } from '../common/Reactive';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, Disposable } from 'vscode';
import fastDeepEqual from 'fast-deep-equal';
import { ILanguageClientHost, LanguageClientHost } from './LanguageClientHost';
import { ICreationKitInfoProvider, ICreationKitInfo } from '../CreationKitInfoProvider';

export interface ILanguageClientManager extends Disposable {
    readonly clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;
}

export class LanguageClientManager implements Disposable, ILanguageClientManager {
    private readonly _clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;

    private readonly _clientSubscriptions: Subscription[];

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @ICreationKitInfoProvider infoProvider: ICreationKitInfoProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        const createClientObservable = (game: PapyrusGame) => {
            return combineLatest(
                configProvider.config.pipe(map((config) => config[game])),
                infoProvider.infos.get(game)
            ).pipe(
                asyncDisposable<[IGameConfig, ICreationKitInfo], LanguageClientHost>(
                    ([gameConfig, creationKitInfo]) =>
                        new LanguageClientHost(game, gameConfig, creationKitInfo, context),
                    (host) => host.start(),
                    fastDeepEqual
                )
            );
        };

        this._clients = new Map([
            ...getGames().map(
                (game) => [game, createClientObservable(game)] as [PapyrusGame, Observable<ILanguageClientHost>]
            ),
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
