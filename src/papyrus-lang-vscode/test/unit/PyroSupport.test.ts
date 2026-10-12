import * as assert from 'assert';
import * as path from 'path';
import { findPyroOnPath, IPyroLocator, IPyroState, IPyroUi, PyroPrompter } from '../../src/features/PyroSupport';

class FakeState implements IPyroState {
    readonly values = new Map<string, unknown>();
    get<T>(key: string): T | undefined {
        return this.values.get(key) as T | undefined;
    }
    async update(key: string, value: unknown): Promise<void> {
        this.values.set(key, value);
    }
}

class FakeUi implements IPyroUi {
    shown: string[] = [];
    releasesOpened = 0;
    saved: string[] = [];
    pick: string | undefined;

    constructor(private readonly _choose: (items: string[]) => string | undefined) {}

    async showMessage(message: string, ...items: string[]): Promise<string | undefined> {
        this.shown.push(message);
        return this._choose(items);
    }
    async openReleases(): Promise<void> {
        this.releasesOpened++;
    }
    async pickExecutable(): Promise<string | undefined> {
        return this.pick;
    }
    async savePath(pyroPath: string): Promise<void> {
        this.saved.push(pyroPath);
    }
}

function locator(configured?: string, onPath?: string): IPyroLocator {
    return { configuredPath: () => configured, findOnPath: () => onPath };
}

const params = { projectPath: '/mods/Foo/Foo.ppj', reasons: ['uses Pyro-only content: <Packages>'] };

describe('PyroPrompter', () => {
    it('stays quiet when Pyro is configured or on the PATH', async () => {
        const ui = new FakeUi(() => undefined);
        assert.strictEqual(await new PyroPrompter(locator('/x/pyro'), new FakeState(), ui).handleProjectNeedsPyro(params), false);
        assert.strictEqual(await new PyroPrompter(locator(undefined, '/usr/bin/pyro'), new FakeState(), ui).handleProjectNeedsPyro(params), false);
        assert.strictEqual(ui.shown.length, 0);
    });

    it('prompts once per project and names the project and reasons', async () => {
        const ui = new FakeUi(() => undefined);
        const prompter = new PyroPrompter(locator(), new FakeState(), ui);

        assert.strictEqual(await prompter.handleProjectNeedsPyro(params), true);
        assert.strictEqual(await prompter.handleProjectNeedsPyro(params), false);
        assert.strictEqual(ui.shown.length, 1);
        assert.ok(ui.shown[0].includes('Foo.ppj'));
        assert.ok(ui.shown[0].includes('<Packages>'));
    });

    it('opens the releases page, locates and saves, or stops asking', async () => {
        const getUi = new FakeUi((items) => items[0]);
        await new PyroPrompter(locator(), new FakeState(), getUi).handleProjectNeedsPyro(params);
        assert.strictEqual(getUi.releasesOpened, 1);

        const locateUi = new FakeUi((items) => items[1]);
        locateUi.pick = '/opt/pyro/pyro';
        await new PyroPrompter(locator(), new FakeState(), locateUi).handleProjectNeedsPyro(params);
        assert.deepStrictEqual(locateUi.saved, ['/opt/pyro/pyro']);

        const state = new FakeState();
        const dismissUi = new FakeUi((items) => items[2]);
        const prompter = new PyroPrompter(locator(), state, dismissUi);
        await prompter.handleProjectNeedsPyro(params);
        assert.strictEqual(state.get('papyrus.pyro.promptDismissed'), true);
        assert.strictEqual(await prompter.handleProjectNeedsPyro({ ...params, projectPath: '/mods/Bar/Bar.ppj' }), false);
    });
});

describe('findPyroOnPath', () => {
    it('finds the first directory on the PATH holding a pyro executable', () => {
        const dirs = ['/a', '/b', '/c'].join(path.delimiter);
        const found = findPyroOnPath(dirs, (candidate) => candidate.startsWith(path.join('/b', 'pyro')));
        assert.ok(found?.startsWith(path.join('/b', 'pyro')));
        assert.strictEqual(findPyroOnPath(dirs, () => false), undefined);
        assert.strictEqual(findPyroOnPath('', () => true), undefined);
    });
});
