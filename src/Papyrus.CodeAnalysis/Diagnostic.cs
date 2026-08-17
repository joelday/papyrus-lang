using System;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

public sealed class Diagnostic
{
    public Diagnostic(DiagnosticInfo info, TextSpan span, SyntaxTree? tree = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        Info = info;
        Span = span;
        Tree = tree;
    }

    public DiagnosticInfo Info { get; }

    public string Id => Info.Id;

    public string Message => Info.Message;

    public TextSpan Span { get; }

    public SyntaxTree? Tree { get; }
}
