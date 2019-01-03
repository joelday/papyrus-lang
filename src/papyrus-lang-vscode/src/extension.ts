import * as vscode from 'vscode';

import { ClientServer } from './ClientServer';
import { SyntaxTreeDataProvider } from './SyntaxTreeDataProvider';
import { SyntaxExplorerView } from './SyntaxExplorerView';

let clientServer: ClientServer;

export async function activate(context: vscode.ExtensionContext) {
    const toolPath = context.asAbsolutePath('./bin/DarkId.Papyrus.Host.exe');

    const compilerAssemblyPath =
        process.platform === 'win32'
            ? vscode.workspace.getConfiguration('papyrus.fallout4').get<string>('compilerAssemblyPath')
            : context.asAbsolutePath('../../dependencies/compiler');

    clientServer = new ClientServer(toolPath, compilerAssemblyPath);
    await clientServer.start();

    if (process.env['PAPYRUS_EXTENSION_DEBUG']) {
        const syntaxTreeDataProvider = new SyntaxTreeDataProvider(clientServer);
        const syntaxExplorer = new SyntaxExplorerView('papyrus-lang-vscode.astTreeView', syntaxTreeDataProvider);
        syntaxExplorer.register();
    }
}

export function deactivate() {
    return clientServer.stop();
}
