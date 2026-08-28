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
}

internal sealed class ClientInfo
{
    public string? Name { get; set; }

    public string? Version { get; set; }
}

internal sealed class ClientCapabilities
{
}

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

internal sealed class InitializedParams
{
}

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

internal sealed class SnapshotParams
{
}

internal sealed class SnapshotResult
{
    public int Generation { get; set; }

    public int OpenDocumentCount { get; set; }

    public int ProjectCount { get; set; }
}
