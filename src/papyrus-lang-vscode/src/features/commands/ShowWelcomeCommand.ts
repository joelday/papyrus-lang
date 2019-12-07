import { Uri, commands } from 'vscode';
import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { IPathResolver } from '../../common/PathResolver';

export class ShowWelcomeCommand extends CommandBase<[Uri]> {
    private readonly _pathResolver: IPathResolver;
    constructor(
        @IPathResolver pathResolver: IPathResolver
    ) {
        super('papyrus.showWelcome');
        this._pathResolver = pathResolver;
    }

    protected async onExecute() {
        const fileUri = Uri.file(await this._pathResolver.getWelcomeFile());

        commands.executeCommand('markdown.showPreview', fileUri);
    }
}
