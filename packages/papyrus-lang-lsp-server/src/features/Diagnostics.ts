import { DiagnosticsError } from 'papyrus-lang/lib/Diagnostics';
import { DiagnosticSeverity, TextDocument } from 'vscode-languageserver';

export function papyrusDiagnosticErrorToDiagnostic(
    error: DiagnosticsError,
    textDocument: TextDocument,
    projectName: string
) {
    return {
        range: {
            start: textDocument.positionAt(error.range.start),
            end: textDocument.positionAt(error.range.end),
        },
        message: `[${projectName}] ${error.message}`,
        source: this._projectName,
        severity: DiagnosticSeverity.Error,
    };
}
