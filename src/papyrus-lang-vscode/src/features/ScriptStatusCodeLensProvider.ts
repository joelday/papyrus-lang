import * as path from 'path';
import { ILanguageClientManager } from '../server/LanguageClientManager';
import { ClientHostStatus } from '../server/LanguageClientHost';
import {
    CodeLens,
    Range,
    Position,
    CodeLensProvider,
    TextDocument,
    CancellationToken,
    Uri,
    Disposable,
    languages,
    EventEmitter,
} from 'vscode';
import { take, mergeMap, distinctUntilChanged } from 'rxjs/operators';
import { Unsubscribable } from 'rxjs';

function createZeroLens() {
    return new CodeLens(new Range(new Position(0, 0), new Position(0, 0)));
}

export class ScriptStatusCodeLensProvider implements CodeLensProvider, Disposable {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _codeLensProviderHandle: Disposable;
    private readonly _onDidChangeCodeLenses: EventEmitter<void>;
    private readonly _languageServerSubscriptions: Unsubscribable[];

    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        this._languageClientManager = languageClientManager;

        // Any server status changes need to trigger the code lenses to refresh.
        this._onDidChangeCodeLenses = new EventEmitter<void>();

        this._codeLensProviderHandle = languages.registerCodeLensProvider(
            { language: 'papyrus', scheme: 'file' },
            this
        );

        this._languageServerSubscriptions = Array.from(this._languageClientManager.clients.values()).map((client) =>
            client
                .pipe(
                    mergeMap((c) => c && c.status),
                    distinctUntilChanged()
                )
                .subscribe({
                    next: () => {
                        this._onDidChangeCodeLenses.fire();
                    },
                })
        );
    }

    async provideCodeLenses(document: TextDocument, cancellationToken: CancellationToken): Promise<CodeLens[]> {
        const clients = await Promise.all(
            Array.from(this._languageClientManager.clients.values()).map((client) => client.pipe(take(1)).toPromise())
        );

        if (cancellationToken.isCancellationRequested) {
            return [];
        }

        const activeClients = (await Promise.all(
            clients.map(async (client) => {
                const clientStatus = await client.status.pipe(take(1)).toPromise();
                if (clientStatus !== ClientHostStatus.running) {
                    return null;
                }

                return client;
            })
        )).filter((client) => client !== null);

        if (cancellationToken.isCancellationRequested) {
            return [];
        }

        if (activeClients.length === 0) {
            const lens = createZeroLens();

            lens.command = {
                title: 'No Papyrus language servers are currently running.',
                command: '',
            };

            return [lens];
        }

        const documentInfos = (await Promise.all(
            activeClients.map((client) => client.getDocumentScriptStatus(document))
        )).filter((documentInfo) => documentInfo !== null);

        if (cancellationToken.isCancellationRequested) {
            return [];
        }

        const unresolvedInAllClients =
            documentInfos.length === 0 || documentInfos.every((info) => info.documentIsUnresolved);
        if (unresolvedInAllClients) {
            const lens = createZeroLens();

            lens.command = {
                title: "Script is not included in a Papyrus project or any of Creation Kit's configured source paths.",
                command: '',
            };

            return [lens];
        }

        const lenses: CodeLens[] = [];

        for (const info of documentInfos) {
            if (info.documentIsOverridden) {
                const overriddenLens = createZeroLens();
                const overridingFile = info.scriptInfo.identifierFiles[0].files[0];

                overriddenLens.command = {
                    title: `Currently overridden by ${path.basename(overridingFile)}. Language features are disabled.`,
                    command: 'vscode.open',
                    arguments: [Uri.file(overridingFile)],
                };

                lenses.push(overriddenLens);
            }
        }

        return lenses;
    }

    get onDidChangeCodeLenses() {
        return this._onDidChangeCodeLenses.event;
    }

    dispose() {
        this._onDidChangeCodeLenses.dispose();

        for (const subscription of this._languageServerSubscriptions) {
            subscription.unsubscribe();
        }

        this._codeLensProviderHandle.dispose();
    }
}
