using System;
using System.Collections.Generic;

namespace Papyrus.CodeAnalysis.Syntax.InternalSyntax;

internal sealed class GreenList : GreenNode
{
    private readonly GreenNode[] _items;

    internal GreenList(GreenNode[] items, DiagnosticInfo[]? diagnostics = null)
        : base(SyntaxKind.List, ComputeWidth(items), ComputeFlags(diagnostics, items), diagnostics)
    {
        _items = items;
    }

    internal override int SlotCount => _items.Length;

    internal override GreenNode? GetSlot(int index)
    {
        return (uint)index < (uint)_items.Length ? _items[index] : null;
    }

    internal override SyntaxNode CreateRed(SyntaxNode? parent, int position)
    {
        throw new InvalidOperationException("Lists are not red nodes.");
    }

    internal override GreenNode WithDiagnostics(DiagnosticInfo[]? diagnostics)
    {
        return new GreenList(_items, diagnostics);
    }

    internal static GreenNode? From(GreenNode? first, GreenNode? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return new GreenList(new[] { first, second });
    }

    internal static GreenNode? From(List<GreenNode> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        return new GreenList(items.ToArray());
    }

    private static int ComputeWidth(GreenNode[] items)
    {
        var width = 0;
        for (var i = 0; i < items.Length; i++)
        {
            width += items[i].FullWidth;
        }

        return width;
    }

    private static NodeFlags ComputeFlags(DiagnosticInfo[]? diagnostics, GreenNode[] items)
    {
        var flags = diagnostics is { Length: > 0 } ? NodeFlags.ContainsDiagnostics : NodeFlags.None;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].ContainsDiagnostics)
            {
                flags |= NodeFlags.ContainsDiagnostics;
            }
        }

        return flags;
    }
}
