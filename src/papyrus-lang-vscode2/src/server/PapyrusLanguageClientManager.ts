import { Disposable } from 'vscode';

/**
 * A tiny, host-independent replacement for `vscode.EventEmitter` that the
 * classes in this module can use to publish state changes without needing a
 * real extension host (or RxJS) to test against.
 */
export class SimpleEmitter<T> {
    private readonly _listeners = new Set<(value: T) => void>();

    readonly event = (listener: (value: T) => void): Disposable => {
        this._listeners.add(listener);
        return { dispose: () => this._listeners.delete(listener) };
    };

    fire(value: T) {
        for (const listener of Array.from(this._listeners)) {
            listener(value);
        }
    }

    dispose() {
        this._listeners.clear();
    }
}

export enum LanguageClientStatus {
    stopped = 'stopped',
    starting = 'starting',
    running = 'running',
    /** The bundled/overridden server executable could not be resolved. */
    serverMissing = 'serverMissing',
    error = 'error',
}

/**
 * The minimal shape of `vscode-languageclient`'s `LanguageClient` that
 * `PapyrusLanguageClientManager` depends on. Depending on this narrow
 * interface (rather than the concrete class) lets unit tests supply a fake
 * client instead of spawning a real language server process.
 */
export interface IManagedLanguageClient extends Disposable {
    start(): Promise<void>;
    stop(): Promise<void>;
}

export interface ILanguageClientFactory {
    (serverPath: string): IManagedLanguageClient;
}

/**
 * Owns the lifecycle of a single `Papyrus.LanguageServer` client for the
 * current workspace.
 *
 * This replaces the old `LanguageClientManager` (which used RxJS's
 * `combineLatest`/a custom `asyncDisposable` operator to recreate one
 * `LanguageClientHost` per game whenever Creation-Kit-derived config
 * changed) with a single client for the whole workspace: the new language
 * server has no per-game ambient configuration to react to (it discovers
 * `.ppj` projects itself), so the only things that can require a restart are
 * the resolved server path changing (e.g. the user edits the
 * `papyrus.server.path` override) or the server executable going missing.
 *
 * Race-safety equivalent to the old RxJS pipeline is provided by
 * `AsyncSerialQueue`: every `start`/`restart`/`stop` request is serialized,
 * so a burst of rapid configuration changes can never leave two clients
 * running or interleave a start with a stop.
 */
export class PapyrusLanguageClientManager implements Disposable {
    private readonly _statusEmitter = new SimpleEmitter<LanguageClientStatus>();
    private readonly _errorEmitter = new SimpleEmitter<string>();
    private _status: LanguageClientStatus = LanguageClientStatus.stopped;
    private _client: IManagedLanguageClient | undefined;
    private _currentServerPath: string | undefined;
    private _isDisposed = false;

    constructor(
        private readonly _resolveServerPath: () => string | undefined,
        private readonly _createClient: ILanguageClientFactory,
        private readonly _serialize: <T>(work: () => Promise<T>) => Promise<T>
    ) {}

    readonly onDidChangeStatus = this._statusEmitter.event;
    readonly onError = this._errorEmitter.event;

    get status(): LanguageClientStatus {
        return this._status;
    }

    get client(): IManagedLanguageClient | undefined {
        return this._status === LanguageClientStatus.running ? this._client : undefined;
    }

    /** Starts the client, or restarts it if the resolved server path has changed. */
    async ensureStarted(): Promise<void> {
        await this._serialize(async () => this._ensureStartedCore());
    }

    async stop(): Promise<void> {
        await this._serialize(async () => this._stopCore());
    }

    private async _ensureStartedCore(): Promise<void> {
        if (this._isDisposed) {
            return;
        }

        const serverPath = this._resolveServerPath();

        if (this._client && serverPath === this._currentServerPath) {
            // Already running the right binary; nothing to do.
            return;
        }

        if (this._client) {
            await this._stopClient();
        }

        if (!serverPath) {
            this._setStatus(LanguageClientStatus.serverMissing);
            return;
        }

        this._currentServerPath = serverPath;
        this._setStatus(LanguageClientStatus.starting);

        try {
            const client = this._createClient(serverPath);
            this._client = client;
            await client.start();

            if (this._isDisposed) {
                await client.stop();
                return;
            }

            this._setStatus(LanguageClientStatus.running);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            this._errorEmitter.fire(message);
            this._setStatus(LanguageClientStatus.error);
        }
    }

    private async _stopCore(): Promise<void> {
        await this._stopClient();
        this._setStatus(LanguageClientStatus.stopped);
    }

    private async _stopClient(): Promise<void> {
        const client = this._client;
        this._client = undefined;
        this._currentServerPath = undefined;

        if (client) {
            await client.stop();
        }
    }

    private _setStatus(status: LanguageClientStatus) {
        this._status = status;
        this._statusEmitter.fire(status);
    }

    dispose() {
        this._isDisposed = true;
        // Best-effort: the caller is tearing down the extension, so we do not
        // await the stop -- but we do still ask the client to shut down.
        void this._stopClient();
        this._statusEmitter.dispose();
        this._errorEmitter.dispose();
    }
}
