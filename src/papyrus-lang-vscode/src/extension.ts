import * as vscode from 'vscode';
import * as path from 'path';

import { ClientServer } from './ClientServer';
import { SyntaxTreeDataProvider } from './SyntaxTreeDataProvider';
import { SyntaxExplorerView } from './SyntaxExplorerView';

let clientServer: ClientServer;

async function getDocumentScriptStatus(document: vscode.TextDocument) {
    const scriptInfo = await clientServer.requestScriptInfo(document.uri.toString());

    const documentIsUnresolved = scriptInfo.identifiers.length === 0;
    const documentIsOverridden =
        !documentIsUnresolved &&
        !scriptInfo.identifierFiles.some((identifierFile) =>
            identifierFile.files.some((file) => file.toLowerCase() === document.uri.fsPath.toLowerCase())
        );

    return {
        documentIsUnresolved,
        documentIsOverridden,
        scriptInfo,
    };
}

function createZeroLens() {
    return new vscode.CodeLens(new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 0)));
}

class ScriptStatusCodeLensProvider implements vscode.CodeLensProvider {
    async provideCodeLenses(document: vscode.TextDocument, _: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        const info = await getDocumentScriptStatus(document);
        const lenses: vscode.CodeLens[] = [];

        if (info.documentIsUnresolved) {
            const lens = createZeroLens();

            lens.command = {
                title: "Not included in a Papyrus project or CreationKit's configured source paths.",
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

        return lenses;
    }
}

export async function activate(context: vscode.ExtensionContext) {
    const toolPath = context.asAbsolutePath('./bin/DarkId.Papyrus.Host.exe');

    const compilerAssemblyPath =
        process.platform === 'win32'
            ? vscode.workspace.getConfiguration('papyrus.fallout4').get<string>('compilerAssemblyPath')
            : context.asAbsolutePath('../../dependencies/compilers/fallout4');

    clientServer = new ClientServer(toolPath, compilerAssemblyPath);
    await clientServer.start();

    if (process.env['PAPYRUS_EXTENSION_DEBUG']) {
        const syntaxTreeDataProvider = new SyntaxTreeDataProvider(clientServer);
        const syntaxExplorer = new SyntaxExplorerView('papyrus-lang-vscode.astTreeView', syntaxTreeDataProvider);
        syntaxExplorer.register();
    }

    var overriddenOrInactiveDecoration = vscode.window.createTextEditorDecorationType({
        opacity: '0.5',
    });

    vscode.window.onDidChangeActiveTextEditor(async (e) => {
        if (e.document.languageId !== 'papyrus') {
            return;
        }

        try {
            const { documentIsUnresolved, documentIsOverridden } = await getDocumentScriptStatus(e.document);

            if (documentIsUnresolved || documentIsOverridden) {
                e.setDecorations(overriddenOrInactiveDecoration, [
                    {
                        range: new vscode.Range(
                            new vscode.Position(0, 0),
                            e.document.positionAt(e.document.getText().length)
                        ),
                    },
                ]);
            } else {
                e.setDecorations(overriddenOrInactiveDecoration, []);
            }
        } catch {
            e.setDecorations(overriddenOrInactiveDecoration, []);
        }
    });

    vscode.languages.registerCodeLensProvider(
        {
            language: 'papyrus',
            scheme: 'file',
        },
        new ScriptStatusCodeLensProvider()
    );
}

export function deactivate() {
    return clientServer.stop();
}
