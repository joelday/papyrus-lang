import * as fs from 'fs';
import * as path from 'path';
import * as crypto from 'crypto';
import { promisify } from 'util';

import procList from 'ps-list';

import { getExecutableNameForGame, PapyrusGame } from './PapyrusGame';

import { isNativeError } from 'util/types';

import { getSystemErrorMap } from 'util';
import { execFile as _execFile } from 'child_process';
const execFile = promisify(_execFile);
const readFile = promisify(fs.readFile);
const writeFile = promisify(fs.writeFile);
const exists = promisify(fs.exists);
const readdir = promisify(fs.readdir);
const stat = promisify(fs.stat);

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

export async function getGamePIDs(game: PapyrusGame): Promise<Array<number>> {
    const processList = await procList();

    const gameProcesses = processList.filter(
        (p) => p.name.toLowerCase() === getExecutableNameForGame(game).toLowerCase()
    );

    if (gameProcesses.length === 0) {
        return [];
    }

    return gameProcesses.map((p) => p.pid);
}

export async function getPIDforProcessName(processName: string): Promise<Array<number>> {
    const processList = await procList();

    const gameProcesses = processList.filter((p) => p.name.toLowerCase() === processName.toLowerCase());

    if (gameProcesses.length === 0) {
        return [];
    }

    return gameProcesses.map((p) => p.pid);
}
export async function getPathFromProcess(pid: number) {
    const pwsh_cmd = `(Get-Process -id ${pid}).Path`;

    const { stdout, stderr } = await execFile('powershell', [pwsh_cmd]);
    if (stderr) {
        return undefined;
    }
    return stdout;
}

export async function getPIDsforFullPath(processPath: string): Promise<Array<number>> {
    const pidsList = await getPIDforProcessName(path.basename(processPath));
    const pids = pidsList.filter(async (pid) => {
        return processPath === (await getPathFromProcess(pid));
    });
    return pids;
}

export function inDevelopmentEnvironment() {
    return process.execArgv.some((arg) => arg.startsWith('--inspect-brk'));
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function toCommandLineArgs(obj: any): string[] {
    return ([] as string[]).concat(
        ...Object.keys(obj).map((key) => {
            const value = obj[key];

            if (typeof value === 'undefined' || value === null) {
                return [] as string[];
            }

            return [
                `--${key}`,
                ...(Array.isArray(value) ? value.map((element) => element.toString()) : [value.toString()]),
            ];
        })
    );
}

export async function mkdirIfNeeded(pathname: string) {
    if (await exists(pathname)) {
        return false;
    }
    mkDirByPathSync(pathname);
}

export function isNodeError(obj: unknown): obj is NodeJS.ErrnoException {
    if (isNativeError(obj)) {
        if (
            (obj as NodeJS.ErrnoException).errno !== undefined &&
            getSystemErrorMap().has((obj as NodeJS.ErrnoException)!.errno!)
        ) {
            return true;
        }
    }

    return false;
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
            if (isNodeError(err)) {
                if (err.code === 'EEXIST') {
                    // curDir already exists!
                    return curDir;
                }

                // To avoid `EISDIR` error on Mac and `EACCES`-->`ENOENT` and `EPERM` on Windows.
                if (err.code === 'ENOENT') {
                    // Throw the original parentDir error on curDir `ENOENT` failure.
                    throw new Error(`EACCES: permission denied, mkdir '${parentDir}'`);
                }

                const caughtErr = !err.code || ['EACCES', 'EPERM', 'EISDIR'].indexOf(err.code) > -1;
                if (!caughtErr || (caughtErr && curDir === path.resolve(targetDir))) {
                    throw err; // Throw if it's just the last created dir.
                }
            }
        }

        return curDir;
    }, initDir);
}

// This will replace tokens of the form ${KEY_NAME} with values from an object { 'KEY_NAME': "replacement string" }
export async function copyAndFillTemplate(srcPath: string, dstPath: string, values: { [key: string]: string }) {
    let templStr = (await readFile(srcPath)).toString();
    for (const key in values) {
        templStr = templStr.replace('${' + key + '}', values[key]);
    }
    return writeFile(dstPath, templStr);
}

export interface EnvData {
    [key: string]: string;
}

export async function getEnvFromProcess(pid: number) {
    const pwsh_cmd = `(Get-Process -id ${pid}).StartInfo.EnvironmentVariables.ForEach( { $_.Key + "=" + $_.Value } )`;

    const { stdout, stderr } = await execFile('powershell', [pwsh_cmd]);
    if (stderr) {
        return undefined;
    }
    const otherEnv: EnvData = {};
    stdout.split('\r\n').forEach((line) => {
        const [key, value] = line.split('=');
        if (key && key !== '') {
            otherEnv[key] = value;
        }
    });
    return otherEnv;
}

export async function CheckHash(data: Buffer, expectedHash: string) {
    const hash = crypto.createHash('sha256');
    hash.update(data);
    const actualHash = hash.digest('hex');
    if (expectedHash !== actualHash) {
        return false;
    }
    return true;
}

async function _GetHashOfFolder(folderPath: string, inputHash?: crypto.Hash): Promise<crypto.Hash | undefined> {
    if (!inputHash) {
        return undefined;
    }
    const info = await readdir(folderPath, { withFileTypes: true });
    if (!info || info.length == 0) {
        return undefined;
    }
    for (const item of info) {
        const fullPath = path.join(folderPath, item.name);
        if (item.isFile()) {
            const data = fs.readFileSync(fullPath);
            inputHash.update(data);
        } else if (item.isDirectory()) {
            // recursively walk sub-folders
            await _GetHashOfFolder(fullPath, inputHash);
        }
    }
    return inputHash;
}

export async function GetHashOfFolder(folderPath: string): Promise<string | undefined> {
    return (await _GetHashOfFolder(folderPath, crypto.createHash('sha256')))?.digest('hex');
}

export async function CheckHashOfFolder(folderPath: string, expectedSHA256: string): Promise<boolean> {
    const hash = await GetHashOfFolder(folderPath);
    if (!hash) {
        return false;
    }
    if (hash !== expectedSHA256) {
        return false;
    }
    return true;
}

export async function CheckHashFile(filePath: string, expectedSHA256: string) {
    // get the hash of the file
    if (!(await exists(filePath)) || !(await stat(filePath)).isFile()) {
        return false;
    }
    const buffer = await readFile(filePath);
    if (!buffer) {
        return false;
    }
    const hash = crypto.createHash('sha256');
    hash.update(buffer);
    const actualHash = hash.digest('hex');
    if (expectedSHA256 !== actualHash) {
        return false;
    }
    return true;
}
