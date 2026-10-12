using System;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

public sealed class Diagnostic
{
    public Diagnostic(
        DiagnosticInfo info,
        TextSpan span,
        SyntaxTree? tree = null,
        DiagnosticSeverity? severity = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        Info = info;
        Span = span;
        Tree = tree;
        Severity = severity ?? info.Descriptor.DefaultSeverity;
    }

    public DiagnosticInfo Info { get; }

    public DiagnosticDescriptor Descriptor => Info.Descriptor;

    public DiagnosticSeverity Severity { get; }

    public string Id => Info.Id;

    public string Message => GetMessage();

    public TextSpan Span { get; }

    public SyntaxTree? Tree { get; }

    public string GetMessage() => Info.GetMessage();
}
