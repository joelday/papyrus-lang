import { readFileSync } from 'fs';
import { GlobSync } from 'glob';
import { normalizeSafe as normalize } from 'upath';
import URI from 'vscode-uri';
import { IFileSystem } from './FileSystem';

export class NodeFileSystem implements IFileSystem {
    public readTextFile(uri: string): string {
        return readFileSync(URI.parse(uri).fsPath, { encoding: 'utf8' });
    }

    public findFilesAsUris(globPattern: string): string[] {
        return new GlobSync(normalize(globPattern)).found.map((f) =>
            URI.file(f).toString()
        );
    }
}
