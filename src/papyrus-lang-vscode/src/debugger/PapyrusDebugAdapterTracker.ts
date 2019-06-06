import { DebugAdapterTrackerFactory, DebugSession, DebugAdapterTracker, window, Disposable, debug } from 'vscode';

export class PapyrusDebugAdapterTrackerFactory implements DebugAdapterTrackerFactory, Disposable {
    private readonly _registration: Disposable;

    constructor() {
        this._registration = debug.registerDebugAdapterTrackerFactory('papyrus', this);
    }

    async createDebugAdapterTracker(session: DebugSession): Promise<DebugAdapterTracker> {
        return new PapyrusDebugAdapterTracker(session);
    }

    dispose() {
        this._registration.dispose();
    }
}

export class PapyrusDebugAdapterTracker implements DebugAdapterTracker {
    private readonly _session: DebugSession;

    private _showErrorMessages = true;

    constructor(session: DebugSession) {
        this._session = session;
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

    onExit(code: number | undefined, signal: string | undefined) {
        if (!this._showErrorMessages || this._session.configuration.noop) {
            return;
        }

        if (code) {
            window.showErrorMessage(`Papyrus debugger exited with code: ${code}, signal: ${signal}`);
        }
    }
}
