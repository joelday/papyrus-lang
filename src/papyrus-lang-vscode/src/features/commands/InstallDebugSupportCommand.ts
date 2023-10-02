/* eslint-disable @typescript-eslint/no-explicit-any */
import { IDebugSupportInstallService, DebugSupportInstallState } from '../../debugger/DebugSupportInstallService';
import { window, ProgressLocation } from 'vscode';
import { PapyrusGame, getDisplayNameForGame } from '../../PapyrusGame';
import { GameCommandBase } from './GameCommandBase';
import { getGameIsRunning } from '../../Utilities';
import { waitWhile } from '../../VsCodeUtilities';
import { inject, injectable } from 'inversify';
import { IMO2ConfiguratorService } from '../../debugger/MO2ConfiguratorService';
import { IMO2LauncherDescriptor } from '../../debugger/MO2LaunchDescriptorFactory';

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

@injectable()
export class InstallDebugSupportCommand extends GameCommandBase {
    private readonly _installer: IDebugSupportInstallService;
    private readonly _mo2ConfiguratorService: IMO2ConfiguratorService;
    constructor(
        @inject(IDebugSupportInstallService) installer: IDebugSupportInstallService,
        @inject(IMO2ConfiguratorService) mo2ConfiguratorService: IMO2ConfiguratorService
    ) {
        super('installDebuggerSupport', [PapyrusGame.fallout4, PapyrusGame.skyrimSpecialEdition]);

        this._installer = installer;
        this._mo2ConfiguratorService = mo2ConfiguratorService;
    }

    // TODO: Fix the args
    protected getLauncherDescriptor(...args: [any | undefined]): IMO2LauncherDescriptor | undefined {
        // If we have args, it's a debugger launch.
        if (args.length > 0) {
            // args 0 indicates the launch type
            const launchArgs: any[] = args[0];
            if (launchArgs.length < 1) {
                return;
            }
            const launchType = launchArgs[0] as string;
            if (launchType === 'XSE') {
                // do stuff
            }
            if (launchArgs.length > 1 && launchType === 'MO2') {
                return launchArgs[1] as IMO2LauncherDescriptor;
            }
        }
        return undefined;
    }

    protected async onExecute(game: PapyrusGame, ...args: [any | undefined]) {
        const launcherDescriptor = this.getLauncherDescriptor(...args);
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

                    return launcherDescriptor
                        ? await this._mo2ConfiguratorService.fixDebuggerConfiguration(launcherDescriptor, token)
                        : await this._installer.installPlugin(game, token);
                } catch (error) {
                    window.showErrorMessage(
                        `Failed to install Papyrus debugger support for ${getDisplayNameForGame(game)}: ${error}`
                    );
                }

                return false;
            }
        );

        const currentStatus = launcherDescriptor
            ? await this._mo2ConfiguratorService.getStateFromConfig(launcherDescriptor)
            : await this._installer.getInstallState(game);

        if (installed) {
            if (currentStatus === DebugSupportInstallState.installedAsMod) {
                window.showInformationMessage(
                    `Papyrus debugger support for ${getDisplayNameForGame(game)} installed` +
                        ' to Mod Manager Directory as mod "Papyrus Debug Extension".' +
                        " Don't forget to enable it in the mod manager!",
                    'Ok'
                );
            } else {
                window.showInformationMessage(`Papyrus debugger support for ${getDisplayNameForGame(game)} installed!`);
            }
        }
    }
}
