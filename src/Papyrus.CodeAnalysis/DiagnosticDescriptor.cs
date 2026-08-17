using System;

namespace Papyrus.CodeAnalysis;

public sealed class DiagnosticDescriptor
{
    public DiagnosticDescriptor(
        string id,
        string title,
        string messageFormat,
        string category,
        DiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(messageFormat);
        ArgumentException.ThrowIfNullOrEmpty(category);

        Id = id;
        Title = title;
        MessageFormat = messageFormat;
        Category = category;
        DefaultSeverity = defaultSeverity;
        IsEnabledByDefault = isEnabledByDefault;
    }

    public string Id { get; }

    public string Title { get; }

    public string MessageFormat { get; }

    public string Category { get; }

    public DiagnosticSeverity DefaultSeverity { get; }

    public bool IsEnabledByDefault { get; }
}
