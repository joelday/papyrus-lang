using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

public sealed class SyntaxTree
{
    private readonly GreenNode _root;
    private readonly SourceText _text;
    private SyntaxNode? _redRoot;

    private SyntaxTree(GreenNode root, SourceText text)
    {
        _root = root;
        _text = text;
    }

    public static SyntaxTree Create(SyntaxNode root, SourceText? text = null)
    {
        return Create(root.Green, text ?? SourceText.From(root.ToFullString()));
    }

    internal static SyntaxTree Create(GreenNode root, SourceText? text = null)
    {
        var tree = new SyntaxTree(root, text ?? SourceText.From(root.ToFullString()));
        var red = root.CreateRed(parent: null, position: 0);
        red.SetSyntaxTree(tree);
        tree._redRoot = red;
        return tree;
    }

    internal static SyntaxTree AttachRoot(SyntaxNode root)
    {
        var tree = new SyntaxTree(root.Green, SourceText.From(root.ToFullString()));
        root.SetSyntaxTree(tree);
        tree._redRoot = root;
        return tree;
    }

    public SourceText GetText() => _text;

    public SyntaxNode GetRoot()
    {
        if (_redRoot is not null)
        {
            return _redRoot;
        }

        var red = _root.CreateRed(parent: null, position: 0);
        red.SetSyntaxTree(this);
        _redRoot = red;
        return red;
    }
}
