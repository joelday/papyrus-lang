using System.Collections.Generic;

namespace Papyrus.LanguageServer;

internal sealed class InitializeParams
{
    public int? ProcessId { get; set; }

    public ClientInfo? ClientInfo { get; set; }

    public string? Locale { get; set; }

    public string? RootPath { get; set; }

    public string? RootUri { get; set; }

    public ClientCapabilities? Capabilities { get; set; }

    public string? Trace { get; set; }

    public WorkspaceFolder[]? WorkspaceFolders { get; set; }
}

internal sealed class WorkspaceFolder
{
    public string? Uri { get; set; }

    public string? Name { get; set; }
}

internal sealed class DidChangeWorkspaceFoldersParams
{
    public WorkspaceFoldersChangeEvent? Event { get; set; }
}

internal sealed class WorkspaceFoldersChangeEvent
{
    public WorkspaceFolder[]? Added { get; set; }

    public WorkspaceFolder[]? Removed { get; set; }
}

internal sealed class LogMessageParams
{
    public MessageType Type { get; set; }

    public string Message { get; set; } = "";
}

internal enum MessageType
{
    Error = 1,
    Warning = 2,
    Info = 3,
    Log = 4,
}

internal sealed class ClientInfo
{
    public string? Name { get; set; }

    public string? Version { get; set; }
}

internal sealed class ClientCapabilities { }

internal sealed class InitializeResult
{
    public ServerCapabilities Capabilities { get; set; } = new();

    public ServerInfo? ServerInfo { get; set; }
}

internal sealed class ServerCapabilities
{
    public TextDocumentSyncOptions? TextDocumentSync { get; set; }

    public SemanticTokensOptions? SemanticTokensProvider { get; set; }

    public bool? FoldingRangeProvider { get; set; }

    public bool? SelectionRangeProvider { get; set; }

    public bool? DocumentSymbolProvider { get; set; }

    public bool? DocumentFormattingProvider { get; set; }

    public bool? HoverProvider { get; set; }

    public bool? DefinitionProvider { get; set; }

    public CompletionOptions? CompletionProvider { get; set; }

    public SignatureHelpOptions? SignatureHelpProvider { get; set; }

    public bool? ReferencesProvider { get; set; }

    public RenameOptions? RenameProvider { get; set; }

    public bool? DocumentHighlightProvider { get; set; }

    public bool? InlayHintProvider { get; set; }

    public DiagnosticOptions? DiagnosticProvider { get; set; }

    public WorkspaceServerCapabilities? Workspace { get; set; }
}

internal sealed class WorkspaceServerCapabilities
{
    public WorkspaceFoldersServerCapabilities? WorkspaceFolders { get; set; }
}

internal sealed class WorkspaceFoldersServerCapabilities
{
    public bool Supported { get; set; }

    public bool ChangeNotifications { get; set; }
}

internal sealed class DiagnosticOptions
{
    public bool InterFileDependencies { get; set; }

    public bool WorkspaceDiagnostics { get; set; }
}

internal sealed class RenameOptions
{
    public bool PrepareProvider { get; set; }
}

internal sealed class CompletionOptions
{
    public string[]? TriggerCharacters { get; set; }

    public bool? ResolveProvider { get; set; }
}

internal sealed class SignatureHelpOptions
{
    public string[]? TriggerCharacters { get; set; }
}

internal sealed class FoldingRangeParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class FoldingRange
{
    public int StartLine { get; set; }

    public int EndLine { get; set; }

    public string? Kind { get; set; }
}

internal sealed class SelectionRangeParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position[]? Positions { get; set; }
}

internal sealed class SelectionRange
{
    public Range Range { get; set; } = new();

    public SelectionRange? Parent { get; set; }
}

internal sealed class DocumentSymbolParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class DocumentSymbol
{
    public string Name { get; set; } = "";

    public int Kind { get; set; }

    public Range Range { get; set; } = new();

    public Range SelectionRange { get; set; } = new();

    public DocumentSymbol[] Children { get; set; } = [];
}

internal sealed class DocumentSyntaxTreeParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class DocumentSyntaxTree
{
    public SyntaxTreeNode? Root { get; set; }
}

internal sealed class DocumentFormattingParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public FormattingOptions? Options { get; set; }
}

internal sealed class FormattingOptions
{
    public int TabSize { get; set; } = 4;

    public bool InsertSpaces { get; set; } = true;
}

internal sealed class TextEdit
{
    public Range Range { get; set; } = new();

    public string NewText { get; set; } = "";
}

internal sealed class SyntaxTreeNode
{
    public string Kind { get; set; } = "";

    public string Text { get; set; } = "";

    public Range Range { get; set; } = new();

    public bool IsToken { get; set; }

    public SyntaxTreeNode[] Children { get; set; } = [];
}

internal sealed class SemanticTokensOptions
{
    public SemanticTokensLegend Legend { get; set; } = new();

    public bool Full { get; set; }

    public bool Range { get; set; }
}

internal sealed class SemanticTokensLegend
{
    public string[] TokenTypes { get; set; } = [];

    public string[] TokenModifiers { get; set; } = [];
}

internal sealed class SemanticTokensParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class SemanticTokensRangeParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Range? Range { get; set; }
}

internal sealed class SemanticTokens
{
    public int[] Data { get; set; } = [];
}

internal sealed class TextDocumentSyncOptions
{
    public bool OpenClose { get; set; }

    public TextDocumentSyncKind Change { get; set; }
}

internal enum TextDocumentSyncKind
{
    None = 0,
    Full = 1,
    Incremental = 2,
}

internal sealed class ServerInfo
{
    public string Name { get; set; } = "Papyrus Language Server";

    public string? Version { get; set; }
}

internal sealed class InitializedParams { }

internal sealed class DidOpenTextDocumentParams
{
    public TextDocumentItem? TextDocument { get; set; }
}

internal sealed class TextDocumentItem
{
    public string? Uri { get; set; }

    public string? LanguageId { get; set; }

    public int Version { get; set; }

    public string? Text { get; set; }
}

internal sealed class DidChangeTextDocumentParams
{
    public VersionedTextDocumentIdentifier? TextDocument { get; set; }

    public TextDocumentContentChangeEvent[]? ContentChanges { get; set; }
}

internal sealed class VersionedTextDocumentIdentifier
{
    public string? Uri { get; set; }

    public int Version { get; set; }
}

internal sealed class TextDocumentContentChangeEvent
{
    public Range? Range { get; set; }

    public int? RangeLength { get; set; }

    public string? Text { get; set; }
}

internal sealed class Range
{
    public Position? Start { get; set; }

    public Position? End { get; set; }
}

internal sealed class Position
{
    public int Line { get; set; }

    public int Character { get; set; }
}

internal sealed class DidCloseTextDocumentParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class TextDocumentIdentifier
{
    public string? Uri { get; set; }
}

internal sealed class TextDocumentPositionParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }
}

internal sealed class Hover
{
    public string[] Contents { get; set; } = [];

    public Range? Range { get; set; }
}

internal sealed class DefinitionParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }
}

internal sealed class Location
{
    public string Uri { get; set; } = "";

    public Range Range { get; set; } = new();
}

internal sealed class CompletionParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }
}

internal sealed class CompletionItem
{
    public string Label { get; set; } = "";

    public int Kind { get; set; }

    public string? Detail { get; set; }

    public string[]? CommitCharacters { get; set; }

    public string? InsertText { get; set; }

    /// <summary>1 = PlainText, 2 = Snippet (LSP <c>InsertTextFormat</c>).</summary>
    public int? InsertTextFormat { get; set; }

    public CompletionItemData? Data { get; set; }
}

/// <summary>
/// Enough context to re-resolve the symbol behind a <see cref="CompletionItem"/>
/// during <c>completionItem/resolve</c>, which the protocol delivers with only
/// the item itself (no document/position).
/// </summary>
internal sealed class CompletionItemData
{
    public string Uri { get; set; } = "";

    public int Offset { get; set; }

    public string Name { get; set; } = "";

    public int Kind { get; set; }
}

internal sealed class SignatureHelpParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }
}

internal sealed class SignatureHelp
{
    public SignatureInformation[] Signatures { get; set; } = [];

    public int ActiveSignature { get; set; }

    public int ActiveParameter { get; set; }
}

internal sealed class SignatureInformation
{
    public string Label { get; set; } = "";

    public ParameterInformation[] Parameters { get; set; } = [];
}

internal sealed class ParameterInformation
{
    public string Label { get; set; } = "";
}

internal sealed class ReferenceParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }

    public ReferenceContext? Context { get; set; }
}

internal sealed class ReferenceContext
{
    public bool IncludeDeclaration { get; set; }
}

internal sealed class RenameParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }

    public string? NewName { get; set; }
}

internal sealed class PrepareRenameResult
{
    public Range Range { get; set; } = new();

    public string Placeholder { get; set; } = "";
}

internal sealed class DocumentHighlightParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Position? Position { get; set; }
}

internal sealed class DocumentHighlight
{
    public Range Range { get; set; } = new();

    /// <summary>1 = Text, 2 = Read, 3 = Write (LSP <c>DocumentHighlightKind</c>).</summary>
    public int Kind { get; set; }
}

internal sealed class InlayHintParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }

    public Range? Range { get; set; }
}

internal sealed class InlayHint
{
    public Position Position { get; set; } = new();

    public string Label { get; set; } = "";

    /// <summary>1 = Type, 2 = Parameter (LSP <c>InlayHintKind</c>).</summary>
    public int Kind { get; set; }

    public bool? PaddingRight { get; set; }
}

internal sealed class WorkspaceEdit
{
    public Dictionary<string, TextEdit[]>? Changes { get; set; }
}

/// <summary>
/// Response shape for <c>textDocument/scriptInfo</c>. Mirrors the legacy handler:
/// candidate qualified names for the requested path, per-name shadowing info, and
/// the full list of search paths across every loaded project.
/// </summary>
internal sealed class DocumentScriptInfo
{
    public string[] Identifiers { get; set; } = [];

    public IdentifierFiles[] IdentifierFiles { get; set; } = [];

    public string[] SearchPaths { get; set; } = [];
}

internal sealed class IdentifierFiles
{
    public string? Identifier { get; set; }

    public string[] Files { get; set; } = [];
}

internal sealed class DocumentScriptInfoParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

/// <summary>Response shape for <c>papyrus/projectInfos</c>.</summary>
internal sealed class ProjectInfos
{
    public ProjectInfo[] Projects { get; set; } = [];
}

internal sealed class ProjectInfo
{
    public string? Name { get; set; }

    public ProjectInfoSourceInclude[] SourceIncludes { get; set; } = [];
}

internal sealed class ProjectInfoSourceInclude
{
    public string? Name { get; set; }

    public string? FullPath { get; set; }

    public bool IsImport { get; set; }

    public ProjectInfoScript[] Scripts { get; set; } = [];
}

internal sealed class ProjectInfoScript
{
    public string? Identifier { get; set; }

    public string? FilePath { get; set; }
}

internal sealed class ProjectInfosParams { }

/// <summary>
/// Response shape for <c>textDocument/assembly</c>: Caprica/CK-flavor Papyrus
/// assembly (.pas) text for the requested document's compiled script, or null if
/// it could not be resolved or emitted.
/// </summary>
internal sealed class DocumentAssembly
{
    public string? Assembly { get; set; }
}

internal sealed class DocumentAssemblyParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

internal sealed class SnapshotParams { }

internal sealed class SnapshotResult
{
    public int Generation { get; set; }

    public int OpenDocumentCount { get; set; }

    public int ProjectCount { get; set; }
}

/// <summary>
/// Server-to-client notification (<c>textDocument/publishDiagnostics</c>).
/// </summary>
internal sealed class PublishDiagnosticsParams
{
    public string? Uri { get; set; }

    public Diagnostic[]? Diagnostics { get; set; }
}

internal sealed class Diagnostic
{
    public Range? Range { get; set; }

    public int Severity { get; set; }

    public string? Code { get; set; }

    public string? Source { get; set; }

    public string? Message { get; set; }
}

internal sealed class DiagnosticParams
{
    public TextDocumentIdentifier? TextDocument { get; set; }
}

/// <summary>
/// Response shape for the pull-diagnostics request (<c>textDocument/diagnostic</c>, LSP 3.17).
/// </summary>
internal sealed class DocumentDiagnosticReport
{
    public string Kind { get; set; } = "full";

    public Diagnostic[] Items { get; set; } = [];
}
