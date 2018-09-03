import * as path from 'upath';
import URI from 'vscode-uri';

import { IFileSystem } from '../host/FileSystem';
import { ProjectConfig } from './ProjectConfig';

export class Project {
    private readonly _fileSystem: IFileSystem;
    private readonly _config: ProjectConfig;

    constructor(config: ProjectConfig, @IFileSystem fileSystem: IFileSystem) {
        this._config = config;
        this._fileSystem = fileSystem;
    }

    get config() {
        return this._config;
    }

    get filePath() {
        return this._config.filePath;
    }

    public resolveFiles(): ReadonlyMap<string, string> {
        const groups = [
            ...[...this._config.imports]
                .reverse()
                .map((i) => this.findPscFiles(path.normalizeSafe(i), true)),
            this.findPscFiles(
                path.normalizeSafe(this._config.folder.path),
                !this._config.folder.noRecurse
            ),
        ];

        const files = new Map<string, string>();

        for (const group of groups) {
            for (const file of group.files) {
                files.set(file[1], URI.file(file[0]).toString());
            }
        }

        return files;
    }

    private findPscFiles(folderPath: string, recurse: boolean) {
        const paths = this._fileSystem.findFilesAsUris(
            path.join(folderPath, recurse ? '**' : '.', '*.psc')
        );

        return {
            basePath: folderPath,
            files: paths.map((f) => [
                f,
                path.basename(
                    path.relative(folderPath, f).replace(/\/|\\/g, ':'),
                    '.psc'
                ),
            ]),
        };
    }
}
