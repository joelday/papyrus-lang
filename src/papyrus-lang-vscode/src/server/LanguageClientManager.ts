import { createDecorator } from 'decoration-ioc';
import { LanguageClient } from './LanguageClient';
import { PapyrusGame, getGames } from '../PapyrusGame';
import { Observable, Subscription, concat } from 'rxjs';
import { IExtensionConfigProvider, IGameConfig } from '../ExtensionConfigProvider';
import { map } from 'rxjs/operators';
import { asyncDisposable } from '../common/Reactive';
import { IExtensionContext } from '../common/vscode/IocDecorators';
import { ExtensionContext, Disposable } from 'vscode';
import fastDeepEqual from 'fast-deep-equal';
import { ILanguageClientHost, LanguageClientHost } from './LanguageClientHost';

export interface ILanguageClientManager extends Disposable {
    readonly clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;
}

export class LanguageClientManager implements Disposable, ILanguageClientManager {
    private readonly _configProvider: IExtensionConfigProvider;
    private readonly _clients: ReadonlyMap<PapyrusGame, Observable<ILanguageClientHost>>;

    private readonly _clientSubscriptions: Subscription[];

    constructor(
        @IExtensionConfigProvider configProvider: IExtensionConfigProvider,
        @IExtensionContext context: ExtensionContext
    ) {
        this._configProvider = configProvider;

        const createClientObservable = (game: PapyrusGame) => {
            return concat(
                this._configProvider.config.pipe(
                    map((config) => config[game]),
                    asyncDisposable<IGameConfig, LanguageClientHost>(
                        (gameConfig) => new LanguageClientHost(game, gameConfig, context),
                        (host) => host.start(),
                        fastDeepEqual
                    )
                )
            ) as Observable<ILanguageClientHost>;
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
