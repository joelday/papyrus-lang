import { PapyrusGame, getDisplayNameForGame } from "../../PapyrusGame";
import { QuickPickItem, window, workspace } from "vscode";
import { CommandBase } from "../../common/vscode/commands/CommandBase";

export class LocateOrDisableGameCommand extends CommandBase {
    private readonly _game: PapyrusGame;

    constructor(game: PapyrusGame) {
        super(`papyrus.locateOrDisable.${game}`);
        this._game = game;
    }

    protected async onExecute() {
        const locate: QuickPickItem = {
            label: `Select install directory...`,
            detail: `Manually find your ${getDisplayNameForGame(this._game)} install directory.`,
            alwaysShow: true,
        };

        const disable: QuickPickItem = {
            label: `Disable ${getDisplayNameForGame(this._game)} language service`,
            detail: `Can be reenabled by changing or removing 'papyrus.${this._game}.enabled' in your global settings.`,
            alwaysShow: true,
        };

        const selected = await window.showQuickPick([locate, disable], {});

        if (selected === locate) {
            const location = await window.showOpenDialog({
                canSelectFiles: false,
                canSelectFolders: true,
                canSelectMany: false,
            });

            if (location && location.length > 0) {
                await workspace
                    .getConfiguration('papyrus')
                    .update(`${this._game}.installPath`, location[0].fsPath, true);
            }
        } else if (selected === disable) {
            await workspace.getConfiguration('papyrus').update(`${this._game}.enabled`, false, true);
        }
    }
}