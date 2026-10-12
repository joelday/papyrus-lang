using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;
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

        using var activity = PapyrusActivitySource.Start(
            "parse",
            [new KeyValuePair<string, object?>("dialect", dialect.Game.ToString())]);
        PapyrusEventSource.Log.Parse();

        var root = new Parser(text, dialect, options, cancellationToken).ParseScript();
        return Create(root.Green, text, dialect, options);
    }

    /// <summary>
    /// The empty tree standing in for a script that has no source (PEX-backed or missing):
    /// it has no text to be wrong about, so it reports no syntax errors.
    /// </summary>
    internal static SyntaxTree CreatePlaceholder(PapyrusDialect dialect, PapyrusParseOptions options)
    {
        var text = SourceText.From(string.Empty);
        var root = new Parser(text, dialect, options, CancellationToken.None) { ReportSyntaxErrors = false }.ParseScript();
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

    public ImmutableArray<Diagnostic> GetDiagnostics(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_root.ContainsDiagnostics)
        {
            return ImmutableArray<Diagnostic>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        CollectDiagnostics(_root, position: 0, builder, cancellationToken);
        var diagnostics = builder.ToImmutable();
        PapyrusEventSource.Log.AddDiagnostics(diagnostics);
        return diagnostics;
    }

    internal SyntaxTree WithRootDiagnostics(params DiagnosticInfo[] diagnostics)
    {
        return Create(_root.SetDiagnostics(diagnostics), _text, Dialect, Options);
    }

    private void CollectDiagnostics(
        GreenNode node,
        int position,
        ImmutableArray<Diagnostic>.Builder builder,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = node.GetDiagnostics();
        if (diagnostics is { Length: > 0 })
        {
            var leading = node.GetLeadingTriviaWidth();
            var trailing = node.GetTrailingTriviaWidth();
            var length = node.FullWidth - leading - trailing;
            var span = new TextSpan(position + leading, length < 0 ? 0 : length);
            for (var i = 0; i < diagnostics.Length; i++)
            {
                builder.Add(new Diagnostic(diagnostics[i], span, this));
            }
        }

        if (node is GreenToken token)
        {
            if (token.Leading is { } leadingTrivia)
            {
                CollectDiagnostics(leadingTrivia, position, builder, cancellationToken);
            }

            if (token.Trailing is { } trailingTrivia)
            {
                CollectDiagnostics(
                    trailingTrivia,
                    position + node.FullWidth - trailingTrivia.FullWidth,
                    builder,
                    cancellationToken);
            }

            return;
        }

        var offset = 0;
        var slotCount = node.SlotCount;
        for (var i = 0; i < slotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is null)
            {
                continue;
            }

            CollectDiagnostics(child, position + offset, builder, cancellationToken);
            offset += child.FullWidth;
        }
    }

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
