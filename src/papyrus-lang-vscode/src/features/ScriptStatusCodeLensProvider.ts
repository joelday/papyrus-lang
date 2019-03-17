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
} from 'vscode';
import { getShortDisplayNameForGame } from '../PapyrusGame';
import { take } from 'rxjs/operators';

function createZeroLens() {
    return new CodeLens(new Range(new Position(0, 0), new Position(0, 0)));
}

export class ScriptStatusCodeLensProvider implements CodeLensProvider, Disposable {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _codeLensProviderHandle: Disposable;

    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        this._languageClientManager = languageClientManager;
        this._codeLensProviderHandle = languages.registerCodeLensProvider(
            { language: 'papyrus', scheme: 'file' },
            this
        );
    }

    async provideCodeLenses(document: TextDocument, cancellationToken: CancellationToken): Promise<CodeLens[]> {
        const clients = await Promise.all(
            Array.from(this._languageClientManager.clients.values()).map((client) => client.pipe(take(1)).toPromise())
        );

        if (cancellationToken.isCancellationRequested) {
            return;
        }

        const documentInfos = (await Promise.all(
            clients.map(async (client) => {
                const clientStatus = await client.status.pipe(take(1)).toPromise();
                if (clientStatus !== ClientHostStatus.running) {
                    return null;
                }

                return await client.getDocumentScriptStatus(document);
            })
        )).filter((documentInfo) => documentInfo !== null);

        if (cancellationToken.isCancellationRequested) {
            return;
        }

        if (documentInfos.length === 0) {
            const lens = createZeroLens();

            lens.command = {
                title: 'No Papyrus language servers are currently running.',
                command: '',
            };

            return [lens];
        }

        const unresolvedInAllClients = documentInfos.every((info) => info.documentIsUnresolved);
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
            if (info.documentIsUnresolved) {
                continue;
            }

            const serverLens = createZeroLens();

            serverLens.command = {
                title: `Active in ${getShortDisplayNameForGame(info.game)} as ${info.scriptInfo.identifiers
                    .map((identifier) => `'${identifier}'`)
                    .join(', ')}`,
                command: '',
            };

            lenses.push(serverLens);

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

    dispose() {
        this._codeLensProviderHandle.dispose();
    }
}
