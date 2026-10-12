import { Disposable, Event } from 'vscode';

/**
 * Minimal shape of `vscode.workspace.onDidChangeConfiguration` that this
 * module depends on. Depending on this narrow structural type (rather than
 * `typeof vscode.workspace`) lets tests supply a plain fake event emitter
 * instead of needing a real extension host.
 */
export interface IConfigurationChangeSource<TChangeEvent> {
    readonly onDidChange: Event<TChangeEvent>;
}

/**
 * Watches a single VS Code configuration section for changes without RxJS.
 *
 * The old extension used a custom `eventToValueObservable` helper plus RxJS's
 * `shareReplay` to turn `workspace.onDidChangeConfiguration` into an
 * always-current `Observable<IExtensionConfig>`. This class provides the same
 * "always know the current value, get notified on relevant changes" contract
 * using a plain `vscode.EventEmitter`-shaped `onDidChange` event instead, so
 * consumers (e.g. a language client manager deciding whether to restart) can
 * subscribe with ordinary `Disposable`-returning `.onDidChange(...)` calls.
 */
export class ConfigurationWatcher<TConfig, TChangeEvent = unknown> implements Disposable {
    private readonly _subscription: Disposable;
    private _current: TConfig;

    constructor(
        source: IConfigurationChangeSource<TChangeEvent>,
        private readonly _getCurrent: () => TConfig,
        private readonly _isRelevant: (changeEvent: TChangeEvent) => boolean,
        private readonly _onChange: (current: TConfig, previous: TConfig) => void
    ) {
        this._current = this._getCurrent();
        this._subscription = source.onDidChange((changeEvent) => this._handleChange(changeEvent));
    }

    get current(): TConfig {
        return this._current;
    }

    private _handleChange(changeEvent: TChangeEvent) {
        if (!this._isRelevant(changeEvent)) {
            return;
        }

        const previous = this._current;
        this._current = this._getCurrent();
        this._onChange(this._current, previous);
    }

    dispose() {
        this._subscription.dispose();
    }
}
