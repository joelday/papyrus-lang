using System;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Symbols;

// Weak so holding a symbol does not pin a syntax tree.
public sealed class SyntaxReference
{
    private readonly WeakReference<SyntaxTree> _tree;

    public SyntaxReference(SyntaxTree tree, TextSpan span)
    {
        ArgumentNullException.ThrowIfNull(tree);
        _tree = new WeakReference<SyntaxTree>(tree);
        Span = span;
    }

    public TextSpan Span { get; }

    public SyntaxTree? SyntaxTree => _tree.TryGetTarget(out var tree) ? tree : null;
}
