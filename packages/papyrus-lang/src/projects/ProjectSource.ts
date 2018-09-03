import { createDecorator } from 'decoration-ioc';
import * as path from 'upath';
import URI from 'vscode-uri';
import { iterateMany } from '../common/Utilities';
import { IFileSystem } from '../host/FileSystem';
import { IProjectConfigParser } from './ProjectConfigParser';

export interface IProjectSource {
    findProjectFiles(rootUris: string[]);
    loadProjectFile(uri: string);
}

export class ProjectSource implements IProjectSource {
    private readonly _fileSystem: IFileSystem;
    private readonly _projectConfigParser: IProjectConfigParser;

    constructor(
        @IFileSystem fileSystem: IFileSystem,
        @IProjectConfigParser projectConfigParser: IProjectConfigParser
    ) {
        this._fileSystem = fileSystem;
        this._projectConfigParser = projectConfigParser;
    }

    public findProjectFiles(rootUris: string[]) {
        return Array.from(
            new Set(
                iterateMany<string>(
                    rootUris.map((uri) =>
                        this._fileSystem.findFilesAsUris(
                            path.normalize(
                                path.join(URI.file(uri).fsPath, '**', '*.ppj')
                            )
                        )
                    )
                )
            ).values()
        );
    }

    public loadProjectFile(uri: string) {
        const filePath = URI.parse(uri).fsPath;
        const resolvedPath = path.resolve(filePath);
        const base = path.dirname(resolvedPath);
        const projectXml = this._fileSystem.readTextFile(uri);

        const projectConfig = this._projectConfigParser.parseConfig(projectXml);
        projectConfig.folder.path = URI.file(
            path.resolve(base, path.normalizeSafe(projectConfig.folder.path))
        ).toString();

        projectConfig.imports = projectConfig.imports
            .map((importPath) => path.normalizeSafe(importPath))
            .map(
                (importPath) =>
                    path.isAbsolute(importPath)
                        ? importPath
                        : path.resolve(base, importPath)
            )
            .map((importPath) => URI.file(importPath).toString());

        projectConfig.filePath = URI.file(filePath).toString();

        if (projectConfig.output) {
            projectConfig.output = URI.file(
                path.normalizeSafe(projectConfig.output)
            ).toString();
        }

        return projectConfig;
    }
}

// tslint:disable-next-line:variable-name
export const IProjectSource = createDecorator<IProjectSource>('projectSource');
