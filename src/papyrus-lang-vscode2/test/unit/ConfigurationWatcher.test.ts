import * as assert from 'assert';
import { Disposable, Event } from 'vscode';
import { ConfigurationWatcher, IConfigurationChangeSource } from '../../src/configuration/ConfigurationWatcher';

/**
 * A minimal fake of `vscode.workspace.onDidChangeConfiguration` -- no real
 * `vscode` module or extension host is needed to exercise
 * `ConfigurationWatcher`'s logic.
 */
class FakeChangeSource<TChangeEvent> implements IConfigurationChangeSource<TChangeEvent> {
    private readonly _listeners = new Set<(e: TChangeEvent) => void>();

    onDidChange: Event<TChangeEvent> = (listener) => {
        this._listeners.add(listener);
        return { dispose: () => this._listeners.delete(listener) } as Disposable;
    };

    fire(changeEvent: TChangeEvent) {
        for (const listener of this._listeners) {
            listener(changeEvent);
        }
    }
}

describe('ConfigurationWatcher', () => {
    it('captures the current value at construction time', () => {
        let value = 'initial';
        const watcher = new ConfigurationWatcher(
            new FakeChangeSource<boolean>(),
            () => value,
            () => true,
            () => undefined
        );

        assert.strictEqual(watcher.current, 'initial');
        value = 'changed-without-event';
        assert.strictEqual(watcher.current, 'initial');
    });

    it('ignores changes the predicate reports as irrelevant', () => {
        const source = new FakeChangeSource<boolean>();
        let value = 'a';
        let changeCount = 0;

        const watcher = new ConfigurationWatcher(
            source,
            () => value,
            (isRelevant) => isRelevant,
            () => {
                changeCount++;
            }
        );

        value = 'b';
        source.fire(false);

        assert.strictEqual(changeCount, 0);
        assert.strictEqual(watcher.current, 'a');
        watcher.dispose();
    });

    it('updates current and notifies on relevant changes', () => {
        const source = new FakeChangeSource<boolean>();
        let value = 'a';
        const seen: Array<{ current: string; previous: string }> = [];

        const watcher = new ConfigurationWatcher(
            source,
            () => value,
            (isRelevant) => isRelevant,
            (current, previous) => {
                seen.push({ current, previous });
            }
        );

        value = 'b';
        source.fire(true);

        assert.strictEqual(watcher.current, 'b');
        assert.deepStrictEqual(seen, [{ current: 'b', previous: 'a' }]);
        watcher.dispose();
    });

    it('stops listening after dispose', () => {
        const source = new FakeChangeSource<boolean>();
        let value = 'a';
        let changeCount = 0;

        const watcher = new ConfigurationWatcher(
            source,
            () => value,
            () => true,
            () => {
                changeCount++;
            }
        );

        watcher.dispose();
        value = 'b';
        source.fire(true);

        assert.strictEqual(changeCount, 0);
        assert.strictEqual(watcher.current, 'a');
    });
});
