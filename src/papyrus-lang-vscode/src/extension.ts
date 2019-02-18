import * as vscode from 'vscode';
import * as path from 'path';

import { ClientServer } from './ClientServer';
import { PapyrusExtension } from './PapyrusExtension';
import { SyntaxTreeDataProvider } from './SyntaxTreeDataProvider';
import { SyntaxExplorerView } from './SyntaxExplorerView';

export const CompilerChannel: vscode.OutputChannel = vscode.window.createOutputChannel('Papyrus Compiler');

let clientServer: ClientServer;
let extension: PapyrusExtension;

var overriddenOrInactiveDecoration = vscode.window.createTextEditorDecorationType({
    opacity: '0.5',
});

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

async function updateTextEditorDecorations(editor: vscode.TextEditor) {
    if (editor.document.languageId !== 'papyrus') {
        return;
    }

    try {
        const { documentIsUnresolved, documentIsOverridden } = await getDocumentScriptStatus(editor.document);

        if (documentIsUnresolved || documentIsOverridden) {
            editor.setDecorations(overriddenOrInactiveDecoration, [
                {
                    range: new vscode.Range(
                        new vscode.Position(0, 0),
                        editor.document.positionAt(editor.document.getText().length)
                    ),
                },
            ]);
        } else {
            editor.setDecorations(overriddenOrInactiveDecoration, []);
        }
    } catch {
        editor.setDecorations(overriddenOrInactiveDecoration, []);
    }
}

function createZeroLens() {
    return new vscode.CodeLens(new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 0)));
}

class ScriptStatusCodeLensProvider implements vscode.CodeLensProvider {
    async provideCodeLenses(document: vscode.TextDocument, _: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        const lenses: vscode.CodeLens[] = [];

        if (clientServer.isActive()) {
            const info = await getDocumentScriptStatus(document);

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
                title: "There is no folder or project currently active. Extension partially disabled.",
                command: '',
            };

            lenses.push(lens);
        }

        return lenses;
    }
}

function getToolPath(context: vscode.ExtensionContext, languageVariantName: string) {
    return context.asAbsolutePath(`./bin/${languageVariantName}/net461/DarkId.Papyrus.Host.${languageVariantName}.exe`);
}

export async function activate(context: vscode.ExtensionContext) {
    extension = new PapyrusExtension(context);

    const toolPath = getToolPath(context, 'Fallout4');

    const compilerAssemblyPath =
        process.platform === 'win32' ? extension.Config.GetCompilerPath : context.asAbsolutePath('../../dependencies/compilers/');

    clientServer = new ClientServer(toolPath, compilerAssemblyPath);
    if (vscode.workspace.name !== undefined) {
        await clientServer.start();

        if (process.env['PAPYRUS_EXTENSION_DEBUG']) {
            const syntaxTreeDataProvider = new SyntaxTreeDataProvider(clientServer);
            const syntaxExplorer = new SyntaxExplorerView('papyrus-lang-vscode.astTreeView', syntaxTreeDataProvider);
            syntaxExplorer.register();
        }
    }

    vscode.languages.registerCodeLensProvider(
        { language: 'papyrus', scheme: 'file', },
        new ScriptStatusCodeLensProvider()
    );

    context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(updateTextEditorDecorations));
    updateTextEditorDecorations(vscode.window.activeTextEditor);
}

export function deactivate() {
    return clientServer.stop();
}
