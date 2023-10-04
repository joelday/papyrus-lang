import * as fs from 'fs';
import { promisify } from 'util';

import { xml2js } from 'xml-js';

import { workspace, Uri, RelativePattern } from 'vscode';

import { PyroGameToPapyrusGame } from './features/PyroTaskDefinition';
import { PapyrusGame } from './PapyrusGame';

const readFile = promisify(fs.readFile);

export async function getWorkspaceGameFromProjects(ppjFiles: Uri[]): Promise<PapyrusGame | undefined> {
    let game: string | undefined = undefined;
    if (!ppjFiles) {
        return undefined;
    }

    for (const ppjFile of ppjFiles) {
        game = await getWorkspaceGameFromProjectFile(ppjFile.fsPath);
        if (game) {
            break;
        }
    }

    if (!game || !PyroGameToPapyrusGame[game as keyof typeof PyroGameToPapyrusGame]) {
        return undefined;
    }

    return PyroGameToPapyrusGame[game as keyof typeof PyroGameToPapyrusGame] as unknown as PapyrusGame;
}

export async function getWorkspaceGameFromProjectFile(projectFile: string): Promise<PapyrusGame | undefined> {
    const xml = await readFile(projectFile, { encoding: 'utf-8' });
    // TODO: Annoying type cast here:
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const results = xml2js(xml, { compact: true, trim: true }) as Record<string, any>;

    return results['PapyrusProject']['_attributes']['Game'];
}

export async function getWorkspaceGame(): Promise<PapyrusGame | undefined> {
    if (!workspace.workspaceFolders) {
        return undefined;
    }

    const ppjFiles: Uri[] = await workspace.findFiles(new RelativePattern(workspace.workspaceFolders[0], '**/*.ppj'));
    return getWorkspaceGameFromProjects(ppjFiles);
}
