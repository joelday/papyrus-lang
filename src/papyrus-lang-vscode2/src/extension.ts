import * as fs from 'fs';
import { commands, ConfigurationChangeEvent, Disposable, ExtensionContext, OutputChannel, window, workspace } from 'vscode';
import { AsyncSerialQueue } from './common/AsyncSerialQueue';
import { ConfigurationWatcher } from './configuration/ConfigurationWatcher';
import { LanguageClientStatusBarItem, showOutputChannelCommandId } from './features/LanguageClientStatusBarItem';
import { PapyrusLanguageClientManager } from './server/PapyrusLanguageClientManager';
import { RealLanguageClient } from './server/RealLanguageClient';
import { ServerPathResolver } from './server/ServerPathResolver';

const CONFIGURATION_SECTION = 'papyrus';

function getServerPathOverride(): string | undefined {
    return workspace.getConfiguration(CONFIGURATION_SECTION).get<string>('server.path') || undefined;
}

/**
 * The extension's composition root.
 *
 * There is no inversify container (or any other DI framework) here on
 * purpose: every class in this extension takes its dependencies as
 * constructor parameters typed to small interfaces, so wiring them up is
 * just plain object construction -- `activate` is the one place that needs
 * to know about concrete implementations at all.
 */
class PapyrusExtension implements Disposable {
    private readonly _outputChannel: OutputChannel;
    private readonly _clientManager: PapyrusLanguageClientManager;
    private readonly _configWatcher: ConfigurationWatcher<string | undefined, ConfigurationChangeEvent>;
    private readonly _statusBarItem: LanguageClientStatusBarItem;
    private readonly _showOutputChannelCommand: Disposable;

    constructor(context: ExtensionContext) {
        this._outputChannel = window.createOutputChannel('Papyrus');

        const serverPathResolver = new ServerPathResolver(
            context.extensionUri.fsPath,
            getServerPathOverride,
            (candidatePath) => fs.existsSync(candidatePath)
        );

        const startQueue = new AsyncSerialQueue();

        this._clientManager = new PapyrusLanguageClientManager(
            () => serverPathResolver.resolve(),
            (serverPath) => new RealLanguageClient(serverPath, this._outputChannel),
            (work) => startQueue.enqueue(work)
        );

        this._clientManager.onError((message) => this._outputChannel.appendLine(`Error: ${message}`));

        this._configWatcher = new ConfigurationWatcher(
            { onDidChange: workspace.onDidChangeConfiguration },
            getServerPathOverride,
            (changeEvent) => changeEvent.affectsConfiguration(`${CONFIGURATION_SECTION}.server.path`),
            () => void this._clientManager.ensureStarted()
        );

        this._statusBarItem = new LanguageClientStatusBarItem(this._clientManager);

        this._showOutputChannelCommand = commands.registerCommand(showOutputChannelCommandId, () =>
            this._outputChannel.show()
        );

        void this._clientManager.ensureStarted();
    }

    dispose() {
        this._showOutputChannelCommand.dispose();
        this._statusBarItem.dispose();
        this._configWatcher.dispose();
        this._clientManager.dispose();
        this._outputChannel.dispose();
    }
}

export function activate(context: ExtensionContext) {
    const extension = new PapyrusExtension(context);
    context.subscriptions.push(extension);
}

export function deactivate() {}
