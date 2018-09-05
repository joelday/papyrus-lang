import { IInstantiationService } from 'decoration-ioc';
import { Program } from 'papyrus-lang/lib/program/Program';
import { IProjectLoader } from 'papyrus-lang/lib/projects/ProjectLoader';
import * as path from 'path';
import { Diagnostic, TextDocument } from 'vscode-languageserver';
import URI from 'vscode-uri';
import { papyrusDiagnosticErrorToDiagnostic } from './features/Diagnostics';

export class ProjectHost {
    private _program: Program;
    private readonly _projectName: string;
    private readonly _projectLoader: IProjectLoader;
    private readonly _instantiationService: IInstantiationService;
    private readonly _projectUri: string;

    constructor(
        projectUri: string,
        projectLoader: IProjectLoader,
        instantiationService: IInstantiationService
    ) {
        this._projectUri = projectUri;
        this._projectName = path.basename(URI.parse(projectUri).fsPath);

        this._projectLoader = projectLoader;
        this._instantiationService = instantiationService;

        this.reloadProject();
    }

    get program() {
        return this._program;
    }

    public reloadProject() {
        const projectConfig = this._projectLoader.loadProject(this._projectUri);

        this._program = this._instantiationService.createInstance(
            Program,
            projectConfig
        );
    }

    public refreshProjectFiles() {
        this._program.refreshProjectFiles();
    }

    public getDiagnosticsForDocument(textDocument: TextDocument): Diagnostic[] {
        const scriptFile = this.program.getScriptFileByUri(
            textDocument.uri,
            true
        );

        if (!scriptFile) {
            return [];
        }

        const scriptDiagnostics = scriptFile.scriptNode.diagnostics;
        const typeAndReferenceDiagnostics = scriptFile.validateTypesAndReferences();

        return scriptDiagnostics.errors
            .map((error) =>
                papyrusDiagnosticErrorToDiagnostic(
                    error,
                    textDocument,
                    this._projectName
                )
            )
            .concat(
                typeAndReferenceDiagnostics.errors.map((error) =>
                    papyrusDiagnosticErrorToDiagnostic(
                        error,
                        textDocument,
                        this._projectName
                    )
                )
            );
    }
}
