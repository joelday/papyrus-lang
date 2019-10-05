import { ILanguageClientManager } from '../server/LanguageClientManager';
import { StatusBarItem, Disposable, window, StatusBarAlignment } from 'vscode';
import { PapyrusGame, getGames, getShortDisplayNameForGame, getDisplayNameForGame } from '../PapyrusGame';
import { Observable, Unsubscribable, combineLatest } from 'rxjs';
import { ILanguageClientHost, ClientHostStatus } from '../server/LanguageClientHost';
import { mergeMap, shareReplay } from 'rxjs/operators';
import { eventToValueObservable } from '../common/vscode/reactive/Events';
import { asyncDisposable } from '../common/Reactive';
import { ShowOutputChannelCommand } from './commands/ShowOutputChannelCommand';
import { LocateOrDisableGameCommand } from './commands/LocateOrDisableGameCommand';

class StatusBarItemController implements Disposable {
    private readonly _statusBarItem: StatusBarItem;
    private readonly _hostSubscription: Unsubscribable;
    private readonly _locateOrDisableCommand: LocateOrDisableGameCommand;

    constructor(game: PapyrusGame, languageClientHost: Observable<ILanguageClientHost>, priority: number) {
        this._statusBarItem = window.createStatusBarItem(StatusBarAlignment.Right, priority);

        this._locateOrDisableCommand = new LocateOrDisableGameCommand(game);

        const activeEditor = eventToValueObservable(window.onDidChangeActiveTextEditor, () => window.activeTextEditor);

        const visibleEditors = eventToValueObservable(
            window.onDidChangeVisibleTextEditors,
            () => window.visibleTextEditors
        );

        const hostStatus = languageClientHost.pipe(
            mergeMap((host) => host.status),
            shareReplay(1)
        );

        const hostError = languageClientHost.pipe(
            mergeMap((host) => host.error),
            shareReplay(1)
        );

        const activeDocumentScriptInfo = combineLatest(languageClientHost, hostStatus, activeEditor).pipe(
            mergeMap(async ([host, status, activeEditor]) => {
                if (
                    status === ClientHostStatus.running &&
                    activeEditor &&
                    activeEditor.document.languageId === 'papyrus'
                ) {
                    return await host.client.requestScriptInfo(activeEditor.document.uri.toString());
                }

                return null;
            }),
            shareReplay(1)
        );

        const showOutputChannelCommand = languageClientHost.pipe(
            asyncDisposable((host) => new ShowOutputChannelCommand(game, () => host.outputChannel))
        );

        this._hostSubscription = combineLatest(
            languageClientHost,
            hostStatus,
            hostError,
            activeEditor,
            visibleEditors,
            showOutputChannelCommand,
            activeDocumentScriptInfo
        ).subscribe({
            next: ([
                host,
                status,
                _error,
                activeEditor,
                visibleEditors,
                showOutputChannelCommand,
                activeDocumentScriptInfo,
            ]) => {
                if (
                    !activeEditor ||
                    !visibleEditors ||
                    visibleEditors.length === 0 ||
                    (activeEditor.document.languageId !== 'papyrus' &&
                        activeEditor.document.languageId !== 'papyrus-project')
                ) {
                    this._statusBarItem.hide();
                    return;
                }

                const displayName = getShortDisplayNameForGame(host.game);
                const fullName = getDisplayNameForGame(host.game);

                this._statusBarItem.command = showOutputChannelCommand.name;

                switch (status) {
                    case ClientHostStatus.disabled:
                        this._statusBarItem.hide();
                        return;
                    case ClientHostStatus.starting:
                        this._statusBarItem.text = `${displayName} $(sync)`;
                        this._statusBarItem.tooltip = `${fullName} language service starting...`;
                        break;
                    case ClientHostStatus.running:
                        this._statusBarItem.text = `${displayName} ${
                            activeDocumentScriptInfo && activeDocumentScriptInfo.identifiers.length > 0
                                ? '$(verified)'
                                : '$(check)'
                        }`;
                        this._statusBarItem.tooltip = `${fullName} language service running.`;
                        break;
                    case ClientHostStatus.missing:
                        this._statusBarItem.text = `${displayName} $(alert)`;
                        this._statusBarItem.tooltip = `Unable to locate ${fullName}. Click for more options...`;
                        this._statusBarItem.command = this._locateOrDisableCommand.name;
                        break;
                    case ClientHostStatus.error:
                        this._statusBarItem.text = `${displayName} $(stop)`;
                        this._statusBarItem.tooltip = `Unexpected error while starting ${fullName} language service.`;
                        break;
                }

                this._statusBarItem.show();
            },
        });
    }

    dispose() {
        this._hostSubscription.unsubscribe();
        this._locateOrDisableCommand.dispose();
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
                        [
                            game,
                            new StatusBarItemController(game, this._languageClientManager.clients.get(game), index),
                        ] as [PapyrusGame, StatusBarItemController]
                ),
        ]);
    }

    dispose() {
        for (const controller of this._statusItems.values()) {
            controller.dispose();
        }
    }
}
