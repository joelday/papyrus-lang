import { createDecorator, optional } from 'decoration-ioc';
import * as path from 'upath';
import URI from 'vscode-uri';
import { ICreationKitInisLoader } from '../config/CreationKitInisLoader';
import { createEmptyConfig, ProjectConfig } from './ProjectConfig';
import { IProjectLoader } from './ProjectLoader';

export class AmbientProjectLoader implements IProjectLoader {
    private readonly _inisLoader: ICreationKitInisLoader;

    constructor(
        @optional(ICreationKitInisLoader) inisLoader: ICreationKitInisLoader
    ) {
        this._inisLoader = inisLoader;
    }

    public loadProject(uri: string): ProjectConfig {
        if (!this._inisLoader) {
            return this.getEmptyProject(uri);
        }

        const ini = this._inisLoader.loadInis(uri);
        const papyrusIni = ini.ini.papyrus;

        if (!papyrusIni) {
            return this.getEmptyProject(uri);
        }

        const installPath = URI.parse(ini.creationKitInstallUri).fsPath;

        const sourceDirectoryPath = papyrusIni.sscriptsourcefolder
            ? this.getPathRelativeToInstallPath(
                  installPath,
                  papyrusIni.sscriptsourcefolder.replace(/"/g, '')
              )
            : null;

        const importPathsElements = papyrusIni.sadditionalimports
            ? papyrusIni.sadditionalimports.replace(/"/g, '').split(';')
            : [];

        const elementsWithSubstitutedSource = importPathsElements
            .map(
                (importPath) =>
                    importPath.toLowerCase() === '$(source)'
                        ? sourceDirectoryPath
                        : importPath
            )
            .filter((importPath) => importPath !== null);

        if (
            sourceDirectoryPath &&
            !elementsWithSubstitutedSource.includes(sourceDirectoryPath)
        ) {
            elementsWithSubstitutedSource.push(sourceDirectoryPath);
        }

        const resolvedImportUris = elementsWithSubstitutedSource.map(
            (importPath) =>
                URI.file(
                    this.getPathRelativeToInstallPath(installPath, importPath)
                ).toString()
        );

        resolvedImportUris.push(uri);

        return {
            ...createEmptyConfig(),
            imports: resolvedImportUris,
            folder: null,
        };
    }

    private getPathRelativeToInstallPath(
        normalizedInstallPath: string,
        maybeRelativePath: string
    ) {
        const normalizedRelativePath = path.normalizeSafe(maybeRelativePath);

        if (path.isAbsolute(normalizedRelativePath)) {
            return normalizedRelativePath;
        }

        return path.resolve(
            path.join(normalizedInstallPath, maybeRelativePath)
        );
    }

    private getEmptyProject(uri: string) {
        return {
            ...createEmptyConfig(),
            imports: [],
            folder: { path: uri },
        };
    }
}

// tslint:disable-next-line:variable-name
export const IAmbientProjectLoader = createDecorator<IProjectLoader>(
    'ambientProjectLoader'
);
