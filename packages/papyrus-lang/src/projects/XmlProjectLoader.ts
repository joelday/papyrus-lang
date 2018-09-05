import { createDecorator } from 'decoration-ioc';
import * as path from 'upath';
import URI from 'vscode-uri';
import { IFileSystem } from '../host/FileSystem';
import { IProjectLoader } from './ProjectLoader';
import { IXmlProjectConfigParser } from './XmlProjectConfigParser';

export class XmlProjectLoader implements IProjectLoader {
    private readonly _fileSystem: IFileSystem;
    private readonly _projectConfigParser: IXmlProjectConfigParser;

    constructor(
        @IFileSystem fileSystem: IFileSystem,
        @IXmlProjectConfigParser projectConfigParser: IXmlProjectConfigParser
    ) {
        this._fileSystem = fileSystem;
        this._projectConfigParser = projectConfigParser;
    }

    public loadProject(uri: string) {
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
export const IXmlProjectLoader = createDecorator<IProjectLoader>(
    'xmlProjectLoader'
);
