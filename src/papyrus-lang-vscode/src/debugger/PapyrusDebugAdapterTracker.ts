import { inject, injectable } from 'inversify';
import { DebugAdapterTrackerFactory, DebugSession, DebugAdapterTracker, window, Disposable, debug } from 'vscode';
import { IDebugLauncherService } from './DebugLauncherService';
@injectable()
export class PapyrusDebugAdapterTrackerFactory implements DebugAdapterTrackerFactory, Disposable {
    private readonly _debugLauncher: IDebugLauncherService;
    private readonly _registration: Disposable;

    constructor(@inject(IDebugLauncherService) debugLauncher: IDebugLauncherService) {
        this._debugLauncher = debugLauncher;
        this._registration = debug.registerDebugAdapterTrackerFactory('papyrus', this);
    }

    async createDebugAdapterTracker(session: DebugSession): Promise<DebugAdapterTracker> {
        return new PapyrusDebugAdapterTracker(session, this._debugLauncher);
    }

    dispose() {
        this._registration.dispose();
    }
}
export class PapyrusDebugAdapterTracker implements DebugAdapterTracker {
    private readonly _debugLauncher: IDebugLauncherService;
    private readonly _session: DebugSession;

    private _showErrorMessages = true;

    constructor(session: DebugSession, debugLauncher: IDebugLauncherService) {
        this._debugLauncher = debugLauncher;
        this._session = session;
    }

    onWillStartSession(): void {
        console.log(`session ${this._session.id} will start with ${JSON.stringify(this._session.configuration)}\n`);
    }

    onWillStopSession() {
        this._showErrorMessages = false;
    }

    onError(error: Error) {
        if (!this._showErrorMessages || this._session.configuration.noop) {
            return;
        }

        window.showErrorMessage(`Papyrus debugger error: ${error.toString()}`);
    }

    // debugging stuff
    // onWillReceiveMessage(message: any) {}
    // onDidSendMessage(message: any) {}

    onExit(code: number | undefined, signal: string | undefined) {
        this._debugLauncher.tearDownAfterDebug();
        if (!this._showErrorMessages || this._session.configuration.noop) {
            return;
        }

        if (code) {
            window.showErrorMessage(`Papyrus debugger exited with code: ${code}, signal: ${signal}`);
        }
    }
}
