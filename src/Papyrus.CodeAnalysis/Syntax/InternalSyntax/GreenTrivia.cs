using System;
using System.Text;

namespace Papyrus.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class GreenTrivia : GreenNode
{
    internal GreenTrivia(SyntaxKind kind, string text, DiagnosticInfo[]? diagnostics = null)
        : base(kind, text?.Length ?? 0, ComputeFlags(diagnostics), diagnostics)
    {
        Text = text ?? "";
    }

    internal string Text { get; }

    internal override int SlotCount => 0;

    internal override GreenNode? GetSlot(int index) => null;

    internal override SyntaxNode CreateRed(SyntaxNode? parent, int position)
    {
        throw new InvalidOperationException("Trivia is not a red node.");
    }

    internal override GreenNode WithDiagnostics(DiagnosticInfo[]? diagnostics)
    {
        return new GreenTrivia(Kind, Text, diagnostics);
    }

    internal override void WriteTo(StringBuilder builder)
    {
        builder.Append(Text);
    }

    internal override int GetLeadingTriviaWidth() => 0;

    internal override int GetTrailingTriviaWidth() => 0;

    private static NodeFlags ComputeFlags(DiagnosticInfo[]? diagnostics)
    {
        return diagnostics is { Length: > 0 } ? NodeFlags.ContainsDiagnostics : NodeFlags.None;
    }
}
