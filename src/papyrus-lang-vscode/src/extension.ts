import * as vscode from 'vscode';

import { PapyrusExtension } from './PapyrusExtension';

export const CompilerChannel: vscode.OutputChannel = vscode.window.createOutputChannel('Papyrus Compiler');

let extension: PapyrusExtension;

export async function activate(context: vscode.ExtensionContext) {
    extension = new PapyrusExtension(context);
}

export function deactivate() {
    return extension.deactivate();
}
