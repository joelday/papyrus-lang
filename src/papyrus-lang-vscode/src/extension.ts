import * as fs from 'fs';
import { commands, ConfigurationChangeEvent, Disposable, env, ExtensionContext, OutputChannel, Uri, window, workspace } from 'vscode';
import { AsyncSerialQueue } from './common/AsyncSerialQueue';
import { ConfigurationWatcher } from './configuration/ConfigurationWatcher';
import { AssemblyView } from './features/AssemblyView';
import { LanguageClientStatusBarItem, showOutputChannelCommandId } from './features/LanguageClientStatusBarItem';
import { registerShowReferencesCommand } from './features/ShowReferencesCommand';
import { discordUrl, IWelcomeUi, showWelcomeCommandId, showWelcomeIfNew } from './features/Welcome';
import {
    findPyroOnPath,
    IPyroUi,
    locatePyroCommandId,
    ProjectNeedsPyroParams,
    projectNeedsPyroMethod,
    PyroPrompter,
    pyroReleasesUrl,
} from './features/PyroSupport';
import { PapyrusLanguageClientManager } from './server/PapyrusLanguageClientManager';
import { RealLanguageClient } from './server/RealLanguageClient';
import { ServerPathResolver } from './server/ServerPathResolver';

const CONFIGURATION_SECTION = 'papyrus';

function getServerPathOverride(): string | undefined {
    return workspace.getConfiguration(CONFIGURATION_SECTION).get<string>('server.path') || undefined;
}

function getPyroPath(): string | undefined {
    const configured = workspace.getConfiguration(CONFIGURATION_SECTION).get<string>('pyro.path') || undefined;
    return configured && fs.existsSync(configured) ? configured : undefined;
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
    private readonly _assemblyView: AssemblyView;
    private readonly _showReferencesCommand: Disposable;
    private readonly _showWelcomeCommand: Disposable;
    private readonly _locatePyroCommand: Disposable;

    constructor(context: ExtensionContext) {
        this._outputChannel = window.createOutputChannel('Papyrus');

        const serverPathResolver = new ServerPathResolver(
            context.extensionUri.fsPath,
            getServerPathOverride,
            (candidatePath) => fs.existsSync(candidatePath)
        );

        const startQueue = new AsyncSerialQueue();

        const pyroUi: IPyroUi = {
            showMessage: (message, ...items) => window.showInformationMessage(message, ...items),
            openReleases: () => env.openExternal(Uri.parse(pyroReleasesUrl)),
            pickExecutable: async () => {
                const picked = await window.showOpenDialog({
                    canSelectMany: false,
                    openLabel: 'Use this Pyro',
                    title: 'Locate the Pyro executable',
                });
                return picked?.[0]?.fsPath;
            },
            savePath: (pyroPath) =>
                workspace.getConfiguration(CONFIGURATION_SECTION).update('pyro.path', pyroPath, true),
        };
        const pyroPrompter = new PyroPrompter(
            { configuredPath: getPyroPath, findOnPath: () => findPyroOnPath() },
            context.globalState,
            pyroUi
        );
        this._locatePyroCommand = commands.registerCommand(locatePyroCommandId, () => pyroPrompter.locate());

        this._clientManager = new PapyrusLanguageClientManager(
            () => serverPathResolver.resolve(),
            (serverPath) => {
                // Makes it obvious which build a dev host is running.
                const built = fs.statSync(serverPath).mtime.toLocaleString();
                this._outputChannel.appendLine(`Starting ${serverPath} (built ${built})`);
                const client = new RealLanguageClient(serverPath, this._outputChannel);
                client.onNotification(projectNeedsPyroMethod, (params) =>
                    void pyroPrompter.handleProjectNeedsPyro(params as ProjectNeedsPyroParams)
                );
                return client;
            },
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

        this._assemblyView = new AssemblyView(() => this._clientManager.client);
        this._showReferencesCommand = registerShowReferencesCommand();

        const welcomeUi: IWelcomeUi = {
            showMessage: (message, ...items) => window.showInformationMessage(message, ...items),
            openWelcomePage: () =>
                commands.executeCommand('markdown.showPreview', Uri.joinPath(context.extensionUri, 'WELCOME.md')),
            openDiscord: () => env.openExternal(Uri.parse(discordUrl)),
        };
        this._showWelcomeCommand = commands.registerCommand(showWelcomeCommandId, () => welcomeUi.openWelcomePage());
        void showWelcomeIfNew(context.globalState, welcomeUi);

        void this._clientManager.ensureStarted();
    }

    dispose() {
        this._locatePyroCommand.dispose();
        this._showWelcomeCommand.dispose();
        this._assemblyView.dispose();
        this._showReferencesCommand.dispose();
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
