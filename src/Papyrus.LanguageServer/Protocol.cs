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
