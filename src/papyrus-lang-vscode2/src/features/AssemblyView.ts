import {
    commands,
    Disposable,
    EventEmitter,
    TextDocumentContentProvider,
    ViewColumn,
    Uri,
    window,
    workspace,
} from 'vscode';
import { IManagedLanguageClient } from '../server/PapyrusLanguageClientManager';

export const viewAssemblyCommandId = 'papyrus.viewAssembly';

const scheme = 'papyrus-assembly';
const assemblySuffix = '.disassemble.pas';
const refreshDelayMs = 500;

interface DocumentAssembly {
    assembly?: string | null;
}

/**
 * "View Assembly": opens a read-only `papyrus-assembly:` document holding the server's
 * disassembly (`textDocument/assembly`) of a script, refreshed as the script is edited.
 */
export class AssemblyView implements TextDocumentContentProvider, Disposable {
    private readonly _onDidChange = new EventEmitter<Uri>();
    private readonly _pending = new Map<string, ReturnType<typeof setTimeout>>();
    private readonly _registrations: Disposable[];

    readonly onDidChange = this._onDidChange.event;

    constructor(private readonly _getClient: () => IManagedLanguageClient | undefined) {
        this._registrations = [
            workspace.registerTextDocumentContentProvider(scheme, this),
            commands.registerCommand(viewAssemblyCommandId, (uri?: Uri) => this.open(uri)),
            workspace.onDidChangeTextDocument((e) => {
                if (e.document.languageId === 'papyrus') {
                    this.scheduleRefresh(e.document.uri);
                }
            }),
        ];
    }

    async provideTextDocumentContent(uri: Uri): Promise<string> {
        const client = this._getClient();
        if (!client) {
            return '; The Papyrus language server is not running.';
        }

        const scriptUri = Uri.file(uri.path.slice(0, -assemblySuffix.length) + '.psc');
        try {
            const result = await client.sendRequest<DocumentAssembly>('textDocument/assembly', {
                textDocument: { uri: scriptUri.toString() },
            });
            return result.assembly ?? '; No assembly: the script is not in a loaded project or does not compile.';
        } catch (error) {
            return `; Failed to get assembly: ${error instanceof Error ? error.message : String(error)}`;
        }
    }

    dispose() {
        for (const timer of this._pending.values()) {
            clearTimeout(timer);
        }

        this._registrations.forEach((r) => r.dispose());
        this._onDidChange.dispose();
    }

    private async open(uri?: Uri) {
        const source =
            uri ?? (window.activeTextEditor?.document.languageId === 'papyrus' ? window.activeTextEditor.document.uri : undefined);
        if (!source || !source.path.toLowerCase().endsWith('.psc')) {
            return;
        }

        const document = await workspace.openTextDocument(toAssemblyUri(source));
        await window.showTextDocument(document, { preview: true, preserveFocus: true, viewColumn: ViewColumn.Beside });
    }

    private scheduleRefresh(source: Uri) {
        const assemblyUri = toAssemblyUri(source);
        const key = assemblyUri.toString();
        if (!workspace.textDocuments.some((d) => d.uri.toString() === key)) {
            return;
        }

        clearTimeout(this._pending.get(key));
        this._pending.set(
            key,
            setTimeout(() => {
                this._pending.delete(key);
                this._onDidChange.fire(assemblyUri);
            }, refreshDelayMs)
        );
    }
}

function toAssemblyUri(source: Uri): Uri {
    return source.with({ scheme, path: source.path.slice(0, -'.psc'.length) + assemblySuffix });
}
