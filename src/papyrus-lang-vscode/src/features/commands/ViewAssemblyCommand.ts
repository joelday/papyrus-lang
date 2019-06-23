import { Uri, commands, window } from 'vscode';
import { CommandBase } from '../../common/vscode/commands/CommandBase';

export class ViewAssemblyCommand extends CommandBase<[Uri]> {
    constructor() {
        super('papyrus.viewAssembly');
    }

    protected async onExecute(uri: Uri) {
        const requestUri =
            uri ||
            (window.activeTextEditor &&
                window.activeTextEditor.document.languageId === 'papyrus' &&
                window.activeTextEditor.document.uri);

        if (!requestUri) {
            return;
        }

        const assemblyUri = requestUri.with({
            scheme: 'papyrus-assembly',
            path: requestUri.path.replace('.psc', '.disassemble.pas'),
        });

        commands.executeCommand('vscode.open', assemblyUri);
    }
}
