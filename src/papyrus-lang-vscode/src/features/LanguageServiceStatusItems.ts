import { ILanguageClientManager } from '../server/LanguageClientManager';
import { StatusBarItem, Disposable, window, StatusBarAlignment, TextEditor } from 'vscode';
import { PapyrusGame, getGames, getShortDisplayNameForGame, getDisplayNameForGame } from '../PapyrusGame';
import { Observable, Unsubscribable, combineLatest as combineLatest, ObservableInput, ObservedValueOf } from 'rxjs';
import { ILanguageClientHost, ClientHostStatus } from '../server/LanguageClientHost';
import { mergeMap, shareReplay } from 'rxjs/operators';
import { eventToValueObservable } from '../common/vscode/reactive/Events';
import { asyncDisposable } from '../common/Reactive';
import { ShowOutputChannelCommand } from './commands/ShowOutputChannelCommand';
import { LocateOrDisableGameCommand } from './commands/LocateOrDisableGameCommand';
import { inject, injectable } from 'inversify';
import { DocumentScriptInfo } from '../server/messages/DocumentScriptInfo';

/* eslint-disable @typescript-eslint/no-explicit-any */
declare function combineLatest_<
    O1 extends ObservableInput<any>,
    O2 extends ObservableInput<any>,
    O3 extends ObservableInput<any>,
    O4 extends ObservableInput<any>,
    O5 extends ObservableInput<any>,
    O6 extends ObservableInput<any>,
    O7 extends ObservableInput<any>,
>(
    ...sources: [O1, O2, O3, O4, O5, O6, O7]
): Observable<
    [
        ObservedValueOf<O1>,
        ObservedValueOf<O2>,
        ObservedValueOf<O3>,
        ObservedValueOf<O4>,
        ObservedValueOf<O5>,
        ObservedValueOf<O6>,
        ObservedValueOf<O7>,
    ]
>;
/* eslint-enable @typescript-eslint/no-explicit-any */
declare type ExtendedCombineLatestSignature = typeof combineLatest_;

class StatusBarItemController implements Disposable {
    private readonly _statusBarItem: StatusBarItem;
    private readonly _hostSubscription: Unsubscribable;
    private readonly _locateOrDisableCommand: LocateOrDisableGameCommand;

    constructor(game: PapyrusGame, languageClientHost: Observable<ILanguageClientHost>, priority: number) {
        this._statusBarItem = window.createStatusBarItem(StatusBarAlignment.Right, priority);

        this._locateOrDisableCommand = new LocateOrDisableGameCommand(game);

        const activeEditor = eventToValueObservable(
            window.onDidChangeActiveTextEditor,
            () => window.activeTextEditor,
            undefined,
            true
        );

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
                    return (await host.client?.requestScriptInfo(activeEditor.document.uri.toString())) || null;
                }

                return null;
            }),
            shareReplay(1)
        );

        const showOutputChannelCommand = languageClientHost.pipe(
            asyncDisposable((host) => new ShowOutputChannelCommand(game, () => host.outputChannel))
        );

        // TODO: Need to update RxJS, combineLatest here doesn't have enough overloads to support the typings here.
        this._hostSubscription = (combineLatest as ExtendedCombineLatestSignature)<
            Observable<ILanguageClientHost>,
            Observable<ClientHostStatus>,
            Observable<string | null>,
            Observable<TextEditor | undefined>,
            Observable<readonly TextEditor[]>,
            Observable<ShowOutputChannelCommand>,
            Observable<DocumentScriptInfo | null>
        >(
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

                const shortName = getShortDisplayNameForGame(host.game);
                const fullName = getDisplayNameForGame(host.game);

                this._statusBarItem.command = showOutputChannelCommand.name;

                switch (status) {
                    case ClientHostStatus.disabled:
                        this._statusBarItem.hide();
                        return;
                    case ClientHostStatus.starting:
                        this._statusBarItem.text = `${shortName} $(sync)`;
                        this._statusBarItem.tooltip = `${fullName} language service starting...`;
                        break;
                    case ClientHostStatus.running: {
                        const hasActiveDocument =
                            activeDocumentScriptInfo && activeDocumentScriptInfo.identifiers.length > 0;

                        this._statusBarItem.text = `${shortName} $(check)` + (hasActiveDocument ? ` $(file-code)` : ``);
                        this._statusBarItem.tooltip = `${fullName} language service is running${
                            hasActiveDocument ? ` and has active scripts.` : `.`
                        }`;
                        break;
                    }
                    case ClientHostStatus.missing:
                        this._statusBarItem.text = `${shortName} $(alert)`;
                        this._statusBarItem.tooltip = `Unable to locate ${fullName}. Click for more options...`;
                        this._statusBarItem.command = this._locateOrDisableCommand.name;
                        break;
                    case ClientHostStatus.compilerMissing:
                        this._statusBarItem.text = `${shortName} $(alert)`;
                        this._statusBarItem.tooltip = `Unable to locate the Papyrus compiler. Make sure that the game install path is correct, Creation Kit has been installed and that, if specified in an ini file, sCompilerFolder is also correct.`;
                        this._statusBarItem.command = this._locateOrDisableCommand.name;
                        break;
                    case ClientHostStatus.error:
                        this._statusBarItem.text = `${shortName} $(stop)`;
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

@injectable()
export class LanguageServiceStatusItems implements Disposable {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _statusItems: Map<PapyrusGame, StatusBarItemController>;

    constructor(@inject(ILanguageClientManager) languageClientManager: ILanguageClientManager) {
        this._languageClientManager = languageClientManager;

        this._statusItems = new Map([
            ...getGames()
                .reverse()
                .map(
                    (game, index) =>
                        [
                            game,
                            new StatusBarItemController(game, this._languageClientManager.clients.get(game)!, index),
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
