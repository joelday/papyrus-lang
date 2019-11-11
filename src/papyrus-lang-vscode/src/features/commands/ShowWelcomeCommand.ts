import * as path from 'path';
import { Uri, commands, ExtensionContext, MarkdownString } from 'vscode';
import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { IExtensionContext } from '../../common/vscode/IocDecorators';
import { getWelcomeFile } from '../../Paths';

export class ShowWelcomeCommand extends CommandBase<[Uri]> {
    private readonly _context: ExtensionContext;
    constructor(
        @IExtensionContext context: ExtensionContext
    ) {
        super('papyrus.showWelcome');
        this._context = context;
    }

    protected async onExecute() {
        const fileUri = Uri.file(
            path.join(this._context.extensionPath, getWelcomeFile())
        );

        commands.executeCommand('markdown.showPreview', fileUri);
    }
}
