import * as fs from 'fs';
import * as path from 'path';

export const locatePyroCommandId = 'papyrus.locatePyro';
export const projectNeedsPyroMethod = 'papyrus/projectNeedsPyro';
export const pyroReleasesUrl = 'https://github.com/fireundubh/pyro/releases/latest';

const dismissedKey = 'papyrus.pyro.promptDismissed';

const getPyroItem = 'Get Pyro';
const locatePyroItem = 'Locate Pyro...';
const dontAskItem = "Don't ask again";

/** Sent by the language server when a loaded `.ppj` relies on Pyro. */
export interface ProjectNeedsPyroParams {
    projectPath: string;
    reasons: string[];
}

/** Where Pyro might be: the `papyrus.pyro.path` setting, or on the PATH. */
export interface IPyroLocator {
    configuredPath(): string | undefined;
    findOnPath(): string | undefined;
}

export interface IPyroState {
    get<T>(key: string): T | undefined;
    update(key: string, value: unknown): Thenable<void>;
}

export interface IPyroUi {
    showMessage(message: string, ...items: string[]): Thenable<string | undefined>;
    openReleases(): Thenable<unknown>;
    /** File picker; resolves to the chosen executable or undefined. */
    pickExecutable(): Thenable<string | undefined>;
    savePath(pyroPath: string): Thenable<void>;
}

/**
 * Pyro is not bundled with the extension. When the server reports that a project
 * relies on it and no Pyro is configured or on the PATH, offer to fetch or locate
 * one, once per project per session.
 */
export class PyroPrompter {
    private readonly _prompted = new Set<string>();

    constructor(
        private readonly _locator: IPyroLocator,
        private readonly _state: IPyroState,
        private readonly _ui: IPyroUi
    ) {}

    /** Returns true if a prompt was shown. */
    async handleProjectNeedsPyro(params: ProjectNeedsPyroParams): Promise<boolean> {
        if (this._locator.configuredPath() || this._locator.findOnPath()) {
            return false;
        }
        if (this._state.get<boolean>(dismissedKey) === true) {
            return false;
        }
        if (this._prompted.has(params.projectPath)) {
            return false;
        }
        this._prompted.add(params.projectPath);

        const choice = await this._ui.showMessage(
            `${path.basename(params.projectPath)} uses Pyro (${params.reasons.join('; ')}). ` +
                'Pyro is not bundled with the extension, but it works alongside it.',
            getPyroItem,
            locatePyroItem,
            dontAskItem
        );

        if (choice === getPyroItem) {
            await this._ui.openReleases();
        } else if (choice === locatePyroItem) {
            await this.locate();
        } else if (choice === dontAskItem) {
            await this._state.update(dismissedKey, true);
        }

        return true;
    }

    /** "Papyrus: Locate Pyro": pick the executable and remember it in settings. */
    async locate(): Promise<string | undefined> {
        const picked = await this._ui.pickExecutable();
        if (picked) {
            await this._ui.savePath(picked);
            await this._state.update(dismissedKey, false);
        }
        return picked;
    }
}

const pyroExecutableNames = process.platform === 'win32' ? ['pyro.exe', 'pyro.cmd', 'pyro.bat'] : ['pyro'];

/** Searches the process PATH for a Pyro executable. */
export function findPyroOnPath(
    envPath: string | undefined = process.env.PATH,
    exists: (candidate: string) => boolean = (candidate) => fs.existsSync(candidate)
): string | undefined {
    for (const dir of (envPath ?? '').split(path.delimiter).filter((d) => d.length > 0)) {
        for (const name of pyroExecutableNames) {
            const candidate = path.join(dir, name);
            if (exists(candidate)) {
                return candidate;
            }
        }
    }
    return undefined;
}
