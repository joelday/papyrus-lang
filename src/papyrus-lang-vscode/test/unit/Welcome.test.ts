import * as assert from 'assert';
import { IWelcomeState, IWelcomeUi, showWelcomeIfNew, welcomeMajor } from '../../src/features/Welcome';

class FakeState implements IWelcomeState {
    readonly values = new Map<string, unknown>();

    get<T>(key: string): T | undefined {
        return this.values.get(key) as T | undefined;
    }

    async update(key: string, value: unknown): Promise<void> {
        this.values.set(key, value);
    }
}

class FakeUi implements IWelcomeUi {
    shown = 0;
    pageOpened = 0;
    discordOpened = 0;

    constructor(private readonly _choose: (items: string[]) => string | undefined) {}

    async showMessage(_message: string, ...items: string[]): Promise<string | undefined> {
        this.shown++;
        return this._choose(items);
    }

    async openWelcomePage(): Promise<void> {
        this.pageOpened++;
    }

    async openDiscord(): Promise<void> {
        this.discordOpened++;
    }
}

describe('showWelcomeIfNew', () => {
    it('shows once per major and remembers even when dismissed', async () => {
        const state = new FakeState();
        const ui = new FakeUi(() => undefined);

        assert.strictEqual(await showWelcomeIfNew(state, ui), true);
        assert.strictEqual(await showWelcomeIfNew(state, ui), false);
        assert.strictEqual(ui.shown, 1);
        assert.strictEqual(state.get('papyrus.welcome.shownForMajor'), welcomeMajor);
    });

    it('shows again after a major bump', async () => {
        const state = new FakeState();
        await state.update('papyrus.welcome.shownForMajor', welcomeMajor - 1);
        const ui = new FakeUi(() => undefined);

        assert.strictEqual(await showWelcomeIfNew(state, ui), true);
    });

    it('opens the welcome page or Discord for the chosen item', async () => {
        const pageUi = new FakeUi((items) => items[0]);
        await showWelcomeIfNew(new FakeState(), pageUi);
        assert.strictEqual(pageUi.pageOpened, 1);
        assert.strictEqual(pageUi.discordOpened, 0);

        const discordUi = new FakeUi((items) => items[1]);
        await showWelcomeIfNew(new FakeState(), discordUi);
        assert.strictEqual(discordUi.pageOpened, 0);
        assert.strictEqual(discordUi.discordOpened, 1);
    });
});
