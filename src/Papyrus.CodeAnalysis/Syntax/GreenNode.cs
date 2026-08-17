using System;
using System.Text;

namespace Papyrus.CodeAnalysis.Syntax;

[Flags]
internal enum NodeFlags : byte
{
    None = 0,
    ContainsDiagnostics = 1 << 0,
    IsMissing = 1 << 1,
}

internal abstract class GreenNode
{
    private readonly NodeFlags _flags;
    private readonly DiagnosticInfo[]? _diagnostics;

    private protected GreenNode(SyntaxKind kind, int fullWidth, NodeFlags flags, DiagnosticInfo[]? diagnostics)
    {
        Kind = kind;
        FullWidth = fullWidth;
        _flags = flags;
        _diagnostics = diagnostics;
    }

    public SyntaxKind Kind { get; }

    public int FullWidth { get; }

    public bool ContainsDiagnostics => (_flags & NodeFlags.ContainsDiagnostics) != 0;

    public bool IsMissing => (_flags & NodeFlags.IsMissing) != 0;

    internal abstract int SlotCount { get; }

    internal DiagnosticInfo[]? GetDiagnostics() => _diagnostics;

    public GreenNode SetDiagnostics(DiagnosticInfo[] diagnostics)
    {
        return WithDiagnostics(diagnostics);
    }

    internal abstract GreenNode WithDiagnostics(DiagnosticInfo[]? diagnostics);

    internal abstract GreenNode? GetSlot(int index);

    internal abstract SyntaxNode CreateRed(SyntaxNode? parent, int position);

    internal virtual void WriteTo(StringBuilder builder)
    {
        var n = SlotCount;
        for (var i = 0; i < n; i++)
        {
            GetSlot(i)?.WriteTo(builder);
        }
    }

    public string ToFullString()
    {
        var builder = new StringBuilder(FullWidth);
        WriteTo(builder);
        return builder.ToString();
    }

    internal virtual int GetLeadingTriviaWidth()
    {
        var n = SlotCount;
        for (var i = 0; i < n; i++)
        {
            var slot = GetSlot(i);
            if (slot is not null)
            {
                return slot.GetLeadingTriviaWidth();
            }
        }

        return 0;
    }

    internal virtual int GetTrailingTriviaWidth()
    {
        for (var i = SlotCount - 1; i >= 0; i--)
        {
            var slot = GetSlot(i);
            if (slot is not null)
            {
                return slot.GetTrailingTriviaWidth();
            }
        }

        return 0;
    }
}
