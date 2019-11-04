import * as fs from 'fs';
import * as path from 'path';
import { promisify } from 'util';

import procList from 'ps-list';

import { workspace, CancellationTokenSource } from 'vscode';
import { PapyrusGame } from './PapyrusGame';
import { getExecutableNameForGame } from './Paths';


const exists = promisify(fs.exists);


export function* flatten<T>(arrs: T[][]): IterableIterator<T> {
    for (const arr of arrs) {
        for (const el of arr) {
            yield el;
        }
    }
}

export function delayAsync(durationMs: number): Promise<void> {
    return new Promise((r) => setTimeout(r, durationMs));
}

export async function getGameIsRunning(game: PapyrusGame) {
    const processList = await procList();
    return processList.some((p) => p.name.toLowerCase() === getExecutableNameForGame(game).toLowerCase());
}

export async function waitWhile(
    func: () => Promise<boolean>,
    cancellationToken = new CancellationTokenSource().token,
    pollingFrequencyMs = 1000
) {
    while ((await func()) && !cancellationToken.isCancellationRequested) {
        await delayAsync(pollingFrequencyMs);
    }
}

export function inDevelopmentEnvironment() {
    return process.execArgv.some((arg) => arg.startsWith('--inspect-brk'));
}

export function toCommandLineArgs(obj: Object): string[] {
    return [].concat(
        ...Object.keys(obj).map((key) => {
            const value = obj[key];

            if (typeof value === 'undefined' || value === null) {
                return [];
            }

            return [
                `--${key}`,
                ...(Array.isArray(value) ? value.map((element) => element.toString()) : [value.toString()]),
            ];
        })
    );
}

export function mkdirIfNeeded(pathname: string) {
    if (fs.existsSync(pathname)) {
        return false;
    }
    mkDirByPathSync(pathname);
}

// Apparently recursive mkdir doesn't want to work on windows.
// Copied this from https://stackoverflow.com/questions/31645738/how-to-create-full-path-with-nodes-fs-mkdirsync
export function mkDirByPathSync(targetDir: string, { isRelativeToScript = false } = {}) {
    const sep = path.sep;
    const initDir = path.isAbsolute(targetDir) ? sep : '';
    const baseDir = isRelativeToScript ? __dirname : '.';

    return targetDir.split(sep).reduce((parentDir, childDir) => {
        const curDir = path.resolve(baseDir, parentDir, childDir);
        try {
            fs.mkdirSync(curDir);
        } catch (err) {
            if (err.code === 'EEXIST') { // curDir already exists!
                return curDir;
            }

            // To avoid `EISDIR` error on Mac and `EACCES`-->`ENOENT` and `EPERM` on Windows.
            if (err.code === 'ENOENT') { // Throw the original parentDir error on curDir `ENOENT` failure.
                throw new Error(`EACCES: permission denied, mkdir '${parentDir}'`);
            }

            const caughtErr = ['EACCES', 'EPERM', 'EISDIR'].indexOf(err.code) > -1;
            if (!caughtErr || caughtErr && curDir === path.resolve(targetDir)) {
                throw err; // Throw if it's just the last created dir.
            }
        }

        return curDir;
    }, initDir);
}

export async function getWorkspaceGame(): Promise<PapyrusGame | undefined> {
    let game = undefined;
    const workspaceRoot = workspace.workspaceFolders[0].uri.fsPath;

    // If Sources/Scripts exists then it's a Skyrim of some sort
    if (await exists(path.join(workspaceRoot, 'Source', 'Scripts'))) {
        // If the executable in the directory above is Skyrim.exe then we assume it's classic.
        if (await exists(path.join(workspaceRoot, '..', getExecutableNameForGame(PapyrusGame.skyrim)))) {
            game = PapyrusGame.skyrim;
        } else {
            // otherwise we assume it's Special Edition
            game = PapyrusGame.skyrimSpecialEdition;
        }
    } else {
        // If the Fallout 4 Scripts/Sources/User exists then we assme it's Fallout 4
        if (await exists(path.join(workspaceRoot, 'Scripts', 'Source', 'User'))) {
            game = PapyrusGame.fallout4;
        }
        // Otherwise it's something we can't identify.
    }

    return game;
}