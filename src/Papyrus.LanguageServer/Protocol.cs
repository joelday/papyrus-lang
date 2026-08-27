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
}

internal sealed class ServerInfo
{
    public string Name { get; set; } = "Papyrus Language Server";

    public string? Version { get; set; }
}

internal sealed class InitializedParams
{
}
