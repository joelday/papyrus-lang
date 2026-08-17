using System;
using System.Text;

namespace Papyrus.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class GreenToken : GreenNode
{
    internal GreenToken(
        SyntaxKind kind,
        string text,
        GreenNode? leading,
        GreenNode? trailing,
        bool isMissing,
        DiagnosticInfo[]? diagnostics = null)
        : base(kind, ComputeWidth(text, leading, trailing), ComputeFlags(diagnostics, isMissing, leading, trailing), diagnostics)
    {
        Text = text ?? "";
        Leading = leading;
        Trailing = trailing;
    }

    internal string Text { get; }

    internal GreenNode? Leading { get; }

    internal GreenNode? Trailing { get; }

    internal override int SlotCount => 0;

    internal override GreenNode? GetSlot(int index) => null;

    internal override SyntaxNode CreateRed(SyntaxNode? parent, int position)
    {
        throw new InvalidOperationException("Tokens are not red nodes.");
    }

    internal override GreenNode WithDiagnostics(DiagnosticInfo[]? diagnostics)
    {
        return new GreenToken(Kind, Text, Leading, Trailing, IsMissing, diagnostics);
    }

    internal override void WriteTo(StringBuilder builder)
    {
        Leading?.WriteTo(builder);
        builder.Append(Text);
        Trailing?.WriteTo(builder);
    }

    internal override int GetLeadingTriviaWidth() => Leading?.FullWidth ?? 0;

    internal override int GetTrailingTriviaWidth() => Trailing?.FullWidth ?? 0;

    private static int ComputeWidth(string text, GreenNode? leading, GreenNode? trailing)
    {
        return (leading?.FullWidth ?? 0) + (text?.Length ?? 0) + (trailing?.FullWidth ?? 0);
    }

    private static NodeFlags ComputeFlags(
        DiagnosticInfo[]? diagnostics,
        bool isMissing,
        GreenNode? leading,
        GreenNode? trailing)
    {
        var flags = NodeFlags.None;
        if (diagnostics is { Length: > 0 })
        {
            flags |= NodeFlags.ContainsDiagnostics;
        }

        if (isMissing)
        {
            flags |= NodeFlags.IsMissing;
        }

        if (leading is not null && leading.ContainsDiagnostics)
        {
            flags |= NodeFlags.ContainsDiagnostics;
        }

        if (trailing is not null && trailing.ContainsDiagnostics)
        {
            flags |= NodeFlags.ContainsDiagnostics;
        }

        return flags;
    }
}
