using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

public abstract class SyntaxNode
{
    private readonly SyntaxNode? _parent;
    private readonly int _position;
    private SyntaxTree? _syntaxTree;

    private protected SyntaxNode(GreenNode green, SyntaxNode? parent, int position)
    {
        Green = green;
        _parent = parent;
        _position = position;
    }

    internal GreenNode Green { get; }

    internal int Position => _position;

    public SyntaxKind Kind => Green.Kind;

    public SyntaxNode? Parent => _parent;

    public SyntaxTree SyntaxTree
    {
        get
        {
            if (_syntaxTree is not null)
            {
                return _syntaxTree;
            }

            if (_parent is not null)
            {
                return _parent.SyntaxTree;
            }

            return SyntaxTree.AttachRoot(this);
        }
    }

    public TextSpan FullSpan => new(_position, Green.FullWidth);

    public TextSpan Span
    {
        get
        {
            var leading = Green.GetLeadingTriviaWidth();
            var trailing = Green.GetTrailingTriviaWidth();
            return new TextSpan(_position + leading, Green.FullWidth - leading - trailing);
        }
    }

    public bool ContainsDiagnostics => Green.ContainsDiagnostics;

    public bool IsMissing => Green.IsMissing;

    public string ToFullString() => Green.ToFullString();

    public abstract SyntaxNode? GetCachedSlot(int index);

    public abstract void SetCachedSlot(int index, SyntaxNode node);

    internal void SetSyntaxTree(SyntaxTree tree)
    {
        _syntaxTree = tree;
    }

    internal T? GetRed<T>(int slot) where T : SyntaxNode
    {
        if (GetCachedSlot(slot) is T cached)
        {
            return cached;
        }

        var green = Green.GetSlot(slot);
        if (green is null)
        {
            return null;
        }

        var red = (T)green.CreateRed(this, _position + GetChildStartOffset(slot));
        SetCachedSlot(slot, red);
        return red;
    }

    internal SyntaxList<T> GetRedList<T>(int slot) where T : SyntaxNode
    {
        return new SyntaxList<T>(Green.GetSlot(slot), this, _position + GetChildStartOffset(slot));
    }

    internal int GetChildStartOffset(int slot)
    {
        var offset = 0;
        for (var i = 0; i < slot; i++)
        {
            offset += Green.GetSlot(i)?.FullWidth ?? 0;
        }

        return offset;
    }
}
