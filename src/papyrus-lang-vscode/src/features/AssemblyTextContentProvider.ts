import { TextDocumentContentProvider, Uri, CancellationToken, Disposable, workspace, window } from 'vscode';
import { ILanguageClientManager } from '../server/LanguageClientManager';

export class AssemblyTextContentProvider implements TextDocumentContentProvider {
    private readonly _languageClientManager: ILanguageClientManager;
    private readonly _registration: Disposable;

    constructor(@ILanguageClientManager languageClientManager: ILanguageClientManager) {
        this._languageClientManager = languageClientManager;

        this._registration = workspace.registerTextDocumentContentProvider('papyrus-assembly', this);
    }

    async provideTextDocumentContent(uri: Uri, cancellationToken: CancellationToken) {
        const scriptUri = Uri.file(uri.fsPath.replace('.disassemble.pas', '.psc'));
        const activeClients = await this._languageClientManager.getActiveLanguageClients(cancellationToken);

        const assemblyResults = await Promise.all(
            activeClients.map((c) => c.client.requestAssembly(scriptUri.toString()))
        );
        const firstValidResult = assemblyResults.find((a) => !!a && !!a.assembly);

        if (!firstValidResult) {
            window.showErrorMessage('Failed to load Papyrus assembly.');
            return;
        }

        return firstValidResult.assembly;
    }

    dispose() {
        this._registration.dispose();
    }
}
