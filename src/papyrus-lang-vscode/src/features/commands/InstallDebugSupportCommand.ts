import { IDebugSupportInstallService, DebugSupportInstallState } from '../../debugger/DebugSupportInstallService';
import { window, ProgressLocation } from 'vscode';
import { PapyrusGame, getDisplayNameForGame } from '../../PapyrusGame';
import { GameCommandBase } from './GameCommandBase';
import { getGameIsRunning, waitWhile } from '../../Utilities';

export enum DebugSupportInstallKind {
    install,
    update,
}

export function showGameDisabledMessage(game: PapyrusGame) {
    window.showErrorMessage(
        `${getDisplayNameForGame(game)} language support must be enabled before installing Papyrus debugger support.`
    );
}

export function showGameMissingMessage(game: PapyrusGame) {
    window.showErrorMessage(
        `Your ${getDisplayNameForGame(
            game
        )} install path must be configured before before installing Papyrus debugger support.`
    );
}

export class InstallDebugSupportCommand extends GameCommandBase<[DebugSupportInstallKind]> {
    private readonly _installer: IDebugSupportInstallService;

    constructor(@IDebugSupportInstallService installer: IDebugSupportInstallService) {
        super('installDebuggerSupport', [PapyrusGame.fallout4, PapyrusGame.skyrimSpecialEdition]);

        this._installer = installer;
    }

    protected async onExecute(game: PapyrusGame, installKind = DebugSupportInstallKind.install) {
        const installed = await window.withProgress(
            {
                cancellable: true,
                location: ProgressLocation.Notification,
                title: 'Papyrus Debugger Support',
            },
            async (progress, token) => {
                try {
                    const currentStatus = await this._installer.getInstallState(game);
                    if (currentStatus === DebugSupportInstallState.gameDisabled) {
                        showGameDisabledMessage(game);
                        return false;
                    }

                    if (currentStatus === DebugSupportInstallState.gameMissing) {
                        showGameMissingMessage(game);
                        return false;
                    }

                    if (await getGameIsRunning(game)) {
                        progress.report({ message: `Please close ${getDisplayNameForGame(game)}.` });
                        await waitWhile(() => getGameIsRunning(game), token);
                    }

                    if (token.isCancellationRequested) {
                        return false;
                    }

                    return await this._installer.installPlugin(game, token);
                } catch (error) {
                    window.showErrorMessage(
                        `Failed to ${
                            installKind === DebugSupportInstallKind.install ? 'install' : 'update'
                        } Papyrus debugger support for ${getDisplayNameForGame(game)}: ${error}`
                    );
                }

                return false;
            }
        );

        if (installed) {
            window.showInformationMessage(
                `Papyrus debugger support for ${getDisplayNameForGame(game)} ${
                    installKind === DebugSupportInstallKind.install ? 'installed' : 'updated'
                }!`
            );
        }
    }
}
