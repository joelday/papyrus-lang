import * as path from 'upath';
import URI from 'vscode-uri';

import { findFiles, iterateMany, readTextFile } from '../common/Utilities';
import { parsePapyrusProjectXml, ProjectConfig } from './ProjectConfig';

export function findProjectFiles(dirPath: string) {
    return findFiles(path.normalize(path.join(dirPath, '**', '*.ppj')));
}

export function findProjectFilesInDirectories(dirPaths: string[]) {
    return Array.from(
        new Set(iterateMany<string>(dirPaths.map(findProjectFiles))).values()
    );
}

export function loadProjectFile(filePath: string) {
    const resolvedPath = path.resolve(filePath);
    const base = path.dirname(resolvedPath);
    const projectXml = readTextFile(filePath);

    const projectConfig = parsePapyrusProjectXml(projectXml);
    projectConfig.folder.path = path.resolve(
        base,
        path.normalizeSafe(projectConfig.folder.path)
    );

    projectConfig.imports = projectConfig.imports
        .map((importPath) => path.normalizeSafe(importPath))
        .map(
            (importPath) =>
                path.isAbsolute(importPath)
                    ? importPath
                    : path.resolve(base, importPath)
        );

    projectConfig.filePath = filePath;

    if (projectConfig.output) {
        projectConfig.output = path.normalizeSafe(projectConfig.output);
    }

    return projectConfig;
}

export class Project {
    private readonly _config: ProjectConfig;

    constructor(config: ProjectConfig) {
        this._config = config;
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
        const paths = findFiles(
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
