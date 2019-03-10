import { ILanguageClientManager } from '../server/LanguageClientManager';
import { StatusBarItem, Disposable, window, StatusBarAlignment } from 'vscode';
import { PapyrusGame, getGames, getShortDisplayNameForGame, getDisplayNameForGame } from '../PapyrusGame';
import { Observable, Unsubscribable, combineLatest } from 'rxjs';
import { ILanguageClientHost, ClientHostStatus } from '../server/LanguageClientHost';
import { mergeMap } from 'rxjs/operators';

class StatusBarItemController implements Disposable {
    private readonly _statusBarItem: StatusBarItem;
    private readonly _hostSubscription: Unsubscribable;

    constructor(languageClientHost: Observable<ILanguageClientHost>, priority: number) {
        this._statusBarItem = window.createStatusBarItem(StatusBarAlignment.Right, priority);

        this._hostSubscription = combineLatest(
            languageClientHost,
            languageClientHost.pipe(mergeMap((host) => host.status)),
            languageClientHost.pipe(mergeMap((host) => host.error))
        ).subscribe({
            next: ([host, status, _]) => {
                const displayName = getShortDisplayNameForGame(host.game);
                const fullName = getDisplayNameForGame(host.game);

                switch (status) {
                    case ClientHostStatus.disabled:
                        this._statusBarItem.hide();
                        return;
                    case ClientHostStatus.starting:
                        this._statusBarItem.text = `${displayName} $(sync)`;
                        this._statusBarItem.tooltip = `Language service starting...`;
                        break;
                    case ClientHostStatus.running:
                        this._statusBarItem.text = `${displayName} $(check)`;
                        this._statusBarItem.tooltip = `Language service running.`;
                        break;
                    case ClientHostStatus.missing:
                        this._statusBarItem.text = `${displayName} $(alert)`;
                        this._statusBarItem.tooltip = `Unable to locate where ${fullName} is installed.`;
                        break;
                    case ClientHostStatus.error:
                        this._statusBarItem.text = `${displayName} $(stop)`;
                        this._statusBarItem.tooltip = `Unexpected error while starting language service.`;
                        break;
                }

                this._statusBarItem.show();
            },
        });
    }

    dispose() {
        this._hostSubscription.unsubscribe();
        this._statusBarItem.dispose();
    }
}

export class LanguageServiceStatusItems implements Disposable {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _statusItems: Map<PapyrusGame, StatusBarItemController>;

    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        this._languageClientManager = languageClientManager;

        this._statusItems = new Map([
            ...getGames()
                .reverse()
                .map(
                    (game, index) =>
                        [game, new StatusBarItemController(this._languageClientManager.clients.get(game), index)] as [
                            PapyrusGame,
                            StatusBarItemController
                        ]
                ),
        ]);
    }

    dispose() {
        for (const controller of this._statusItems.values()) {
            controller.dispose();
        }
    }
}
