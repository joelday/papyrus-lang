import { CommandBase } from '../../common/vscode/commands/CommandBase';
import { IDebugSupportInstaller, DebugSupportInstallState } from '../../debugger/DebugSupportInstaller';
import { window, ProgressLocation } from 'vscode';

export class InstallDebugSupportCommand extends CommandBase {
    private readonly _installer: IDebugSupportInstaller;

    constructor(@IDebugSupportInstaller installer: IDebugSupportInstaller) {
        super('papyrus.fallout4.installDebuggerSupport');

        this._installer = installer;
    }

    protected async onExecute() {
        const installed = await window.withProgress(
            {
                cancellable: true,
                location: ProgressLocation.Notification,
                title: 'Papyrus Debugger Support',
            },
            async (progress, token) => {
                const currentStatus = await this._installer.getInstallState(progress, token);
                if (currentStatus === DebugSupportInstallState.cancelled) {
                    return false;
                }

                if (currentStatus === DebugSupportInstallState.gameDisabled) {
                    window.showErrorMessage(
                        'Fallout 4 language support must be enabled before installing the Papyrus debugger plugin.'
                    );
                    return false;
                }

                if (currentStatus === DebugSupportInstallState.gameMissing) {
                    window.showErrorMessage('Unable to locate Fallout 4 install path.');
                    return false;
                }

                await this._installer.installPlugin(progress, token);

                return true;
            }
        );

        if (installed) {
            window.showInformationMessage('Papyrus debugging support plugin for Fallout 4 installed.');
        }
    }
}
