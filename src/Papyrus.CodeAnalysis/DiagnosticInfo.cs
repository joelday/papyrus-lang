using System;
using System.Globalization;

namespace Papyrus.CodeAnalysis;

public sealed class DiagnosticInfo
{
    private readonly object?[] _arguments;
    private string? _message;

    public DiagnosticInfo(string id, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(message);

        Id = id;
        Descriptor = DiagnosticCatalog.TryGet(id, out var descriptor)
            ? descriptor
            : new DiagnosticDescriptor(
                id,
                id,
                message,
                DiagnosticCatalog.InternalCategory,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true);
        _arguments = [];
        _message = message;
    }

    public DiagnosticInfo(DiagnosticDescriptor descriptor, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        Id = descriptor.Id;
        _arguments = arguments is { Length: > 0 } ? (object?[])arguments.Clone() : [];
    }

    public DiagnosticDescriptor Descriptor { get; }

    public string Id { get; }

    public string Message => GetMessage();

    public string GetMessage()
    {
        if (_message is not null)
        {
            return _message;
        }

        _message = _arguments.Length == 0
            ? Descriptor.MessageFormat
            : string.Format(CultureInfo.InvariantCulture, Descriptor.MessageFormat, _arguments);
        return _message;
    }
}
