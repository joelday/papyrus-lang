using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

public sealed class PapyrusCompilation
{
    public const string UnresolvedScriptDiagnosticId = "PAP1000";

    private readonly PapyrusParseOptions _options;
    private readonly ImmutableDictionary<QualifiedName, SyntaxTree> _overrides;
    private readonly ConcurrentDictionary<QualifiedName, SyntaxTree> _trees;
    private readonly ConcurrentDictionary<QualifiedName, IScriptSymbol> _scripts;

    private PapyrusCompilation(
        PapyrusDialect dialect,
        ScriptResolver resolver,
        NameTable names,
        PapyrusParseOptions options,
        ImmutableDictionary<QualifiedName, SyntaxTree> overrides,
        ConcurrentDictionary<QualifiedName, SyntaxTree> trees,
        ConcurrentDictionary<QualifiedName, IScriptSymbol> scripts)
    {
        Dialect = dialect;
        Resolver = resolver;
        Names = names;
        _options = options;
        _overrides = overrides;
        _trees = trees;
        _scripts = scripts;
    }

    public PapyrusDialect Dialect { get; }

    public ScriptResolver Resolver { get; }

    public NameTable Names { get; }

    public static PapyrusCompilation Create(
        PapyrusDialect dialect,
        ScriptResolver resolver,
        PapyrusParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(resolver);

        return new PapyrusCompilation(
            dialect,
            resolver,
            new NameTable(),
            options ?? PapyrusParseOptions.Default,
            ImmutableDictionary<QualifiedName, SyntaxTree>.Empty,
            new ConcurrentDictionary<QualifiedName, SyntaxTree>(),
            new ConcurrentDictionary<QualifiedName, IScriptSymbol>());
    }

    public IScriptSymbol GetScript(QualifiedName name)
    {
        name = Intern(name);
        return _scripts.GetOrAdd(name, CreateScript);
    }

    public SyntaxTree GetSyntaxTree(QualifiedName name)
    {
        name = Intern(name);
        return _trees.GetOrAdd(name, LoadSyntaxTree);
    }

    public PapyrusCompilation WithSyntaxTree(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (!TryGetDeclaredName(tree, out var name))
        {
            throw new ArgumentException("Syntax tree has no script name.", nameof(tree));
        }

        return ReplaceTree(name, tree);
    }

    public PapyrusCompilation WithScriptText(QualifiedName name, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        name = Intern(name);
        return ReplaceTree(name, SyntaxTree.Parse(text, Dialect, _options));
    }

    public ImmutableArray<Diagnostic> GetDiagnostics(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var tree in _trees.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddRange(tree.GetDiagnostics(cancellationToken));
        }

        return builder.ToImmutable();
    }

    private PapyrusCompilation ReplaceTree(QualifiedName name, SyntaxTree tree)
    {
        var overrides = _overrides.SetItem(name, tree);
        var trees = new ConcurrentDictionary<QualifiedName, SyntaxTree>();
        foreach (var pair in _trees)
        {
            if (!pair.Key.Equals(name))
            {
                trees.TryAdd(pair.Key, pair.Value);
            }
        }

        trees[name] = tree;

        var scripts = new ConcurrentDictionary<QualifiedName, IScriptSymbol>();
        foreach (var pair in _scripts)
        {
            if (!pair.Key.Equals(name))
            {
                scripts.TryAdd(pair.Key, pair.Value);
            }
        }

        scripts[name] = CreateSourceScript(name, tree);

        return new PapyrusCompilation(Dialect, Resolver, Names, _options, overrides, trees, scripts);
    }

    private IScriptSymbol CreateScript(QualifiedName name)
    {
        if (HasSource(name))
        {
            return CreateSourceScript(name, GetSyntaxTree(name));
        }

        if (Resolver.TryResolvePex(name.ToString(), out var metadata) && metadata is not null)
        {
            return PexSymbolAdapter.ToScriptSymbol(metadata, Names, Dialect);
        }

        _ = GetSyntaxTree(name);
        return CreateErrorScript(name);
    }

    private SyntaxTree LoadSyntaxTree(QualifiedName name)
    {
        if (_overrides.TryGetValue(name, out var overrideTree))
        {
            return overrideTree;
        }

        if (Resolver.TryResolve(name, out var text, out _))
        {
            return SyntaxTree.Parse(text, Dialect, _options);
        }

        if (Resolver.TryResolvePex(name.ToString(), out _))
        {
            return SyntaxTree.Parse(SourceText.From(string.Empty), Dialect, _options);
        }

        return CreateUnresolvedTree(name);
    }

    private bool HasSource(QualifiedName name)
    {
        return _overrides.ContainsKey(name) || Resolver.TryResolve(name, out _, out _);
    }

    private SyntaxTree CreateUnresolvedTree(QualifiedName name)
    {
        var tree = SyntaxTree.Parse(SourceText.From(string.Empty), Dialect, _options);
        return tree.WithRootDiagnostics(
            new DiagnosticInfo(
                UnresolvedScriptDiagnosticId,
                "Script '" + name + "' could not be resolved."));
    }

    private IScriptSymbol CreateSourceScript(QualifiedName name, SyntaxTree tree)
    {
        var references = ImmutableArray<SyntaxReference>.Empty;
        if (tree.TryGetRoot(out var root))
        {
            var span = root.Name.IsMissing ? root.Span : root.Name.Span;
            references = ImmutableArray.Create(new SyntaxReference(tree, span));
        }

        return new ScriptSymbol(SimpleName(name), name, new TypeName(name), references);
    }

    private static IScriptSymbol CreateErrorScript(QualifiedName name)
    {
        return new ScriptSymbol(SimpleName(name), name, new TypeName(name));
    }

    private static PapyrusName SimpleName(QualifiedName name)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        return parts.IsEmpty ? PapyrusName.Empty : parts[^1];
    }

    private QualifiedName Intern(QualifiedName name)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        if (parts.IsEmpty)
        {
            return name;
        }

        var builder = ImmutableArray.CreateBuilder<PapyrusName>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            builder.Add(Names.Get(parts[i].Text));
        }

        return new QualifiedName(builder.MoveToImmutable());
    }

    private bool TryGetDeclaredName(SyntaxTree tree, out QualifiedName name)
    {
        name = default;
        if (!tree.TryGetRoot(out var root) || root.Name.IsMissing)
        {
            return false;
        }

        name = Intern(ToQualifiedName(root.Name));
        return name.Parts.Length > 0;
    }

    private QualifiedName ToQualifiedName(NameSyntax name)
    {
        var parts = ImmutableArray.CreateBuilder<PapyrusName>();
        AppendNameParts(name, parts);
        return new QualifiedName(parts.ToImmutable());
    }

    private void AppendNameParts(NameSyntax name, ImmutableArray<PapyrusName>.Builder parts)
    {
        switch (name)
        {
            case IdentifierNameSyntax identifier:
                if (!identifier.IsMissing && identifier.Identifier.Text.Length > 0)
                {
                    parts.Add(Names.Get(identifier.Identifier.Text));
                }

                break;

            case QualifiedNameSyntax qualified:
                AppendNameParts(qualified.Left, parts);
                if (!qualified.Right.IsMissing && qualified.Right.Identifier.Text.Length > 0)
                {
                    parts.Add(Names.Get(qualified.Right.Identifier.Text));
                }

                break;
        }
    }
}
