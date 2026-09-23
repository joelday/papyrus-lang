import { Disposable, StatusBarAlignment, StatusBarItem, window } from 'vscode';
import { LanguageClientStatus, PapyrusLanguageClientManager } from '../server/PapyrusLanguageClientManager';

const SHOW_OUTPUT_CHANNEL_COMMAND = 'papyrus.showOutputChannel';

/**
 * A single status bar item reflecting the one workspace-wide language
 * client's status.
 *
 * The old extension rendered one status bar item per game (fallout4/skyrim/
 * skyrimSpecialEdition), because each had its own ambient, Creation-Kit-
 * detected `LanguageClientHost`. Since the new language server is a single
 * process per workspace that discovers `.ppj` projects itself, only one
 * status item is needed.
 */
export class LanguageClientStatusBarItem implements Disposable {
    private readonly _statusBarItem: StatusBarItem;
    private readonly _subscriptions: Disposable[] = [];

    constructor(private readonly _manager: PapyrusLanguageClientManager) {
        this._statusBarItem = window.createStatusBarItem(StatusBarAlignment.Right, 0);
        this._statusBarItem.command = SHOW_OUTPUT_CHANNEL_COMMAND;

        this._subscriptions.push(this._manager.onDidChangeStatus((status) => this._render(status)));
        this._render(this._manager.status);
    }

    private _render(status: LanguageClientStatus) {
        switch (status) {
            case LanguageClientStatus.stopped:
                this._statusBarItem.hide();
                break;
            case LanguageClientStatus.starting:
                this._statusBarItem.text = 'Papyrus $(sync~spin)';
                this._statusBarItem.tooltip = 'Papyrus language service starting...';
                this._statusBarItem.show();
                break;
            case LanguageClientStatus.running:
                this._statusBarItem.text = 'Papyrus $(check)';
                this._statusBarItem.tooltip = 'Papyrus language service is running.';
                this._statusBarItem.show();
                break;
            case LanguageClientStatus.serverMissing:
                this._statusBarItem.text = 'Papyrus $(alert)';
                this._statusBarItem.tooltip =
                    'Unable to locate the Papyrus language server. Click to view the output channel.';
                this._statusBarItem.show();
                break;
            case LanguageClientStatus.error:
                this._statusBarItem.text = 'Papyrus $(stop)';
                this._statusBarItem.tooltip = 'Unexpected error while starting the Papyrus language service.';
                this._statusBarItem.show();
                break;
        }
    }

    dispose() {
        for (const subscription of this._subscriptions) {
            subscription.dispose();
        }
        this._statusBarItem.dispose();
    }
}

export const showOutputChannelCommandId = SHOW_OUTPUT_CHANNEL_COMMAND;
