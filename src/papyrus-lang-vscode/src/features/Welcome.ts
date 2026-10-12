export const showWelcomeCommandId = 'papyrus.showWelcome';
export const discordUrl = 'https://discord.gg/E4dWujQ';

/** Bump when a new major deserves a fresh one-time welcome. */
export const welcomeMajor = 4;

const shownForMajorKey = 'papyrus.welcome.shownForMajor';

const readMoreItem = 'What changed';
const discordItem = 'Join the Discord';

/** The slice of `vscode.Memento` the welcome needs. */
export interface IWelcomeState {
    get<T>(key: string): T | undefined;
    update(key: string, value: unknown): Thenable<void>;
}

/** The UI the welcome drives, kept narrow so it is unit-testable offline. */
export interface IWelcomeUi {
    showMessage(message: string, ...items: string[]): Thenable<string | undefined>;
    openWelcomePage(): Thenable<unknown>;
    openDiscord(): Thenable<unknown>;
}

/**
 * Shows the one-time "welcome to 4.x" notification on the first activation
 * after an upgrade (or fresh install), and never again for the same major.
 *
 * Returns true if the notification was shown.
 */
export async function showWelcomeIfNew(state: IWelcomeState, ui: IWelcomeUi): Promise<boolean> {
    if (state.get<number>(shownForMajorKey) === welcomeMajor) {
        return false;
    }

    // Recorded before the prompt resolves: a dismissed notification still counts as seen.
    await state.update(shownForMajorKey, welcomeMajor);

    const choice = await ui.showMessage(
        'Welcome to Papyrus 4: a new compiler, a new language server, and Linux and macOS support. ' +
            'See what is new, what is not here yet, and tell us what you think.',
        readMoreItem,
        discordItem
    );

    if (choice === readMoreItem) {
        await ui.openWelcomePage();
    } else if (choice === discordItem) {
        await ui.openDiscord();
    }

    return true;
}
