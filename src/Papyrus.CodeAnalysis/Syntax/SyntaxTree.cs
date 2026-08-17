using System.Threading;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

public sealed class SyntaxTree
{
    private readonly GreenNode _root;
    private readonly SourceText _text;
    private SyntaxNode? _redRoot;

    private SyntaxTree(
        GreenNode root,
        SourceText text,
        PapyrusDialect? dialect,
        PapyrusParseOptions? options)
    {
        _root = root;
        _text = text;
        Dialect = dialect ?? PapyrusDialect.SkyrimSe;
        Options = options ?? PapyrusParseOptions.Default;
    }

    public PapyrusDialect Dialect { get; }

    public PapyrusParseOptions Options { get; }

    public static SyntaxTree Parse(string? text, PapyrusDialect? dialect)
    {
        return Parse(SourceText.From(text ?? string.Empty), dialect);
    }

    public static SyntaxTree Parse(SourceText text, PapyrusParseOptions options)
    {
        return Parse(text, dialect: null, options);
    }

    public static SyntaxTree Parse(
        SourceText? text,
        PapyrusDialect? dialect,
        PapyrusParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        text ??= SourceText.From(string.Empty);
        options ??= PapyrusParseOptions.Default;
        dialect ??= PapyrusDialect.FromOptions(options);

        var root = new Parser(text, dialect, options, cancellationToken).ParseScript();
        return Create(root.Green, text, dialect, options);
    }

    public static SyntaxTree Create(SyntaxNode root, SourceText? text = null)
    {
        return Create(root.Green, text ?? SourceText.From(root.ToFullString()));
    }

    internal static SyntaxTree Create(GreenNode root, SourceText? text = null)
    {
        return Create(root, text ?? SourceText.From(root.ToFullString()), dialect: null, options: null);
    }

    private static SyntaxTree Create(
        GreenNode root,
        SourceText text,
        PapyrusDialect? dialect,
        PapyrusParseOptions? options)
    {
        var tree = new SyntaxTree(root, text, dialect, options);
        var red = root.CreateRed(parent: null, position: 0);
        red.SetSyntaxTree(tree);
        tree._redRoot = red;
        return tree;
    }

    internal static SyntaxTree AttachRoot(SyntaxNode root)
    {
        var tree = new SyntaxTree(root.Green, SourceText.From(root.ToFullString()), dialect: null, options: null);
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

    internal bool TryGetRoot(out ScriptDeclarationSyntax root)
    {
        if (GetRoot() is ScriptDeclarationSyntax script)
        {
            root = script;
            return true;
        }

        root = null!;
        return false;
    }
}
