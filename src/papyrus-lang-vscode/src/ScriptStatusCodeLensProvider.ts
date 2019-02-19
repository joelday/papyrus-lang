import * as vscode from 'vscode';
import * as path from 'path';
import { PapyrusServer } from './PapyrusServer';

function createZeroLens() {
    return new vscode.CodeLens(new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 0)));
}

export class ScriptStatusCodeLensProvider implements vscode.CodeLensProvider {
    private readonly _server: PapyrusServer;

    constructor(server: PapyrusServer) {
        this._server = server;
    }

    async provideCodeLenses(document: vscode.TextDocument, _: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        const lenses: vscode.CodeLens[] = [];

        if (this._server.clientServer.isActive()) {
            const info = await this._server.getDocumentScriptStatus(document);

            if (info.documentIsUnresolved) {
                const lens = createZeroLens();

                lens.command = {
                    title: "Not included in a Papyrus project or Creation Kit's configured source paths.",
                    command: '',
                };

                lenses.push(lens);
            } else if (info.documentIsOverridden) {
                const lens = createZeroLens();
                const overridingFile = info.scriptInfo.identifierFiles[0].files[0];
                const overridingFileUri = vscode.Uri.file(overridingFile);

                lens.command = {
                    title: `Overridden by ${path.basename(overridingFile)}`,
                    command: 'vscode.open',
                    arguments: [overridingFileUri],
                };

                lenses.push(lens);
            }
        } else {
            const lens = createZeroLens();

            lens.command = {
                title: 'There is no folder or project currently active. Extension partially disabled.',
                command: '',
            };

            lenses.push(lens);
        }

        return lenses;
    }
}