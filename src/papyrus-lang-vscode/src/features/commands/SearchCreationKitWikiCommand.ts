import { EditorCommandBase } from '../../common/vscode/commands/EditorCommandBase';
import { TextEditor, env, Uri, window } from 'vscode';
import { ILanguageClientManager } from '../../server/LanguageClientManager';
import { PapyrusGame } from "../../PapyrusGame";
import { inject, injectable } from 'inversify';

@injectable()
export class SearchCreationKitWikiCommand extends EditorCommandBase {
    private readonly _languageClientManager: ILanguageClientManager;

    constructor(@inject(ILanguageClientManager) languageClientManager: ILanguageClientManager) {
        super('papyrus.searchCreationKitWiki');

        this._languageClientManager = languageClientManager;
    }

    protected async onExecute(editor: TextEditor) {
        const { document, selection } = editor;

        if (!selection.isSingleLine) {
            return;
        }

        const range = selection.isEmpty ? document.getWordRangeAtPosition(selection.start) : selection;
        if (!range || range.isEmpty) {
            return;
        }

        const searchText = document.getText(range);
        if (searchText.trim() === '') {
            return;
        }

        const activeClients = await this._languageClientManager.getActiveLanguageClients();
        const documentInfos = await Promise.all(activeClients.map((c) => c.getDocumentScriptStatus(editor.document)));

        const firstActiveDocument = documentInfos.find((docInfo) => !docInfo?.documentIsUnresolved);
        if (!firstActiveDocument) {
            window.showErrorMessage(
                'Failed to open Creation Kit search page because this script is currently unresolved.'
            );

            return;
        }

        const searchUri =
            firstActiveDocument.game === PapyrusGame.fallout4
                ? `https://www.creationkit.com/fallout4/index.php?search=${searchText}`
                : `https://www.creationkit.com/index.php?search=${searchText}`;

        const successful = await env.openExternal(Uri.parse(searchUri));
        if (!successful) {
            window.showErrorMessage('Failed to open Creation Kit search page.');
        }
    }
}
