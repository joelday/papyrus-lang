import * as path from 'upath';
import URI from 'vscode-uri';
import { ICreationKitInisLoader } from '../config/CreationKitInisLoader';
import { createEmptyConfig, ProjectConfig } from './ProjectConfig';
import { IProjectLoader } from './ProjectLoader';

export class AmbientProjectLoader implements IProjectLoader {
    private readonly _inisLoader: ICreationKitInisLoader;

    constructor(@ICreationKitInisLoader inisLoader: ICreationKitInisLoader) {
        this._inisLoader = inisLoader;
    }

    public loadProject(uri: string): ProjectConfig {
        if (this._inisLoader) {
            return this.getEmptyProject(uri);
        }

        const ini = this._inisLoader.loadInis();
        const papyrusIni = ini.ini.papyrus;

        if (!papyrusIni) {
            return this.getEmptyProject(uri);
        }

        const installPath = URI.parse(ini.creationKitInstallUri).fsPath;

        const sourceDirectoryPath = papyrusIni.sscriptsourcefolder
            ? this.getPathRelativeToInstallPath(
                  installPath,
                  papyrusIni.sscriptsourcefolder
              )
            : null;

        const importPathsElements = papyrusIni.sadditionalimports
            ? papyrusIni.sadditionalimports.split(';')
            : [];

        const elementsWithSubstitutedSource = importPathsElements
            .map(
                (importPath) =>
                    importPath.toLowerCase() === '$(source)' &&
                    sourceDirectoryPath
                        ? sourceDirectoryPath
                        : null
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

        resolvedImportUris.unshift(uri);

        return {
            ...createEmptyConfig(),
            imports: resolvedImportUris,
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
