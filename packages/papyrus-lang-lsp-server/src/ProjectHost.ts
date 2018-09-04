import { IInstantiationService } from 'decoration-ioc';
import { Program } from 'papyrus-lang/lib/program/Program';
import { IProjectSource } from 'papyrus-lang/lib/projects/ProjectSource';
import * as path from 'path';
import { Diagnostic, TextDocument } from 'vscode-languageserver';
import URI from 'vscode-uri';
import { papyrusDiagnosticErrorToDiagnostic } from './features/Diagnostics';

export class ProjectHost {
    private _program: Program;
    private readonly _projectName: string;
    private readonly _projectSource: IProjectSource;
    private readonly _instantiationService: IInstantiationService;
    private readonly _projectUri: string;

    constructor(
        projectUri: string,
        @IProjectSource projectSource: IProjectSource,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        this._projectUri = projectUri;
        this._projectName = path.basename(URI.parse(projectUri).fsPath);

        this._projectSource = projectSource;
        this._instantiationService = instantiationService;

        this.reloadProject();
    }

    get program() {
        return this._program;
    }

    public reloadProject() {
        const projectConfig = this._projectSource.loadProjectFile(
            this._projectUri
        );

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
