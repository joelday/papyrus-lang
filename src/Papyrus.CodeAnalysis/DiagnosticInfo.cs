namespace Papyrus.CodeAnalysis;

public sealed class DiagnosticInfo
{
    public DiagnosticInfo(string id, string message)
    {
        Id = id;
        Message = message;
    }

    public string Id { get; }

    public string Message { get; }
}
