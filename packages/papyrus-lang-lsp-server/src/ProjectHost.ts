import { LanguageServiceHost } from 'papyrus-lang/lib/program/LanguageServiceHost';
import { Program } from 'papyrus-lang/lib/program/Program';
import { loadProjectFile } from 'papyrus-lang/lib/program/Project';
import * as path from 'path';
import {
    Diagnostic,
    DiagnosticSeverity,
    TextDocument,
} from 'vscode-languageserver';
import URI from 'vscode-uri';
import { papyrusDiagnosticErrorToDiagnostic } from './features/Diagnostics';

export class ProjectHost {
    private _program: Program;
    private readonly _projectName: string;
    private readonly _languageServiceHost: LanguageServiceHost;
    private readonly _projectPath: string;

    constructor(projectUri: string, languageServiceHost: LanguageServiceHost) {
        this._projectPath = URI.parse(projectUri).fsPath;
        this._projectName = path.basename(this._projectPath);
        this._languageServiceHost = languageServiceHost;

        this.reloadProject();
    }

    get program() {
        return this._program;
    }

    public reloadProject() {
        this._program = new Program(
            loadProjectFile(this._projectPath),
            this._languageServiceHost
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
