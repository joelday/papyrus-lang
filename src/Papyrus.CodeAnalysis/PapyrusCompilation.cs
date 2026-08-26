using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis;

public sealed class PapyrusCompilation
{
    public const string UnresolvedScriptDiagnosticId = DiagnosticCatalog.UnresolvedScriptId;
    public const string UnresolvedBaseTypeDiagnosticId = DiagnosticCatalog.UnresolvedBaseTypeId;
    public const string CyclicBaseTypeDiagnosticId = DiagnosticCatalog.CyclicBaseTypeId;
    public const string UnresolvedTypeDiagnosticId = DiagnosticCatalog.UnresolvedTypeId;
    public const string InvalidIsOperatorDiagnosticId = DiagnosticCatalog.InvalidIsOperatorId;
    public const string CannotConvertDiagnosticId = DiagnosticCatalog.CannotConvertId;
    public const string UnsupportedStructDiagnosticId = DiagnosticCatalog.UnsupportedStructId;
    public const string UnsupportedGroupDiagnosticId = DiagnosticCatalog.UnsupportedGroupId;
    public const string UnsupportedCustomEventDiagnosticId = DiagnosticCatalog.UnsupportedCustomEventId;
    public const string UnsupportedGuardDiagnosticId = DiagnosticCatalog.UnsupportedGuardId;
    public const string UnsupportedNamespaceDiagnosticId = DiagnosticCatalog.UnsupportedNamespaceId;
    public const string ReservedMemberNameDiagnosticId = DiagnosticCatalog.ReservedMemberNameId;

    [ThreadStatic]
    private static List<QualifiedName>? t_binding;

    private readonly PapyrusParseOptions _options;
    private readonly ImmutableDictionary<QualifiedName, SyntaxTree> _overrides;
    private readonly ConcurrentDictionary<QualifiedName, CachedScript> _cache;
    private readonly ConcurrentDictionary<QualifiedName, IScriptSymbol> _scripts;
    private readonly ConcurrentDictionary<QualifiedName, ImmutableHashSet<QualifiedName>> _dependents;
    private readonly ConcurrentDictionary<QualifiedName, int> _versions;
    private readonly ILogger? _logger;

    private PapyrusCompilation(
        PapyrusDialect dialect,
        ScriptResolver resolver,
        NameTable names,
        PapyrusParseOptions options,
        ImmutableDictionary<QualifiedName, SyntaxTree> overrides,
        ConcurrentDictionary<QualifiedName, CachedScript> cache,
        ConcurrentDictionary<QualifiedName, IScriptSymbol> scripts,
        ConcurrentDictionary<QualifiedName, ImmutableHashSet<QualifiedName>> dependents,
        ConcurrentDictionary<QualifiedName, int> versions,
        ILogger? logger)
    {
        Dialect = dialect;
        Resolver = resolver;
        Names = names;
        _options = options;
        _overrides = overrides;
        _cache = cache;
        _scripts = scripts;
        _dependents = dependents;
        _versions = versions;
        _logger = logger;
    }

    public PapyrusDialect Dialect { get; }

    public ScriptResolver Resolver { get; }

    public NameTable Names { get; }

    public PapyrusParseOptions Options => _options;

    public static PapyrusCompilation Create(
        PapyrusDialect dialect,
        ScriptResolver resolver,
        PapyrusParseOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(resolver);

        return new PapyrusCompilation(
            dialect,
            resolver,
            new NameTable(),
            options ?? PapyrusParseOptions.Default,
            ImmutableDictionary<QualifiedName, SyntaxTree>.Empty,
            new ConcurrentDictionary<QualifiedName, CachedScript>(),
            new ConcurrentDictionary<QualifiedName, IScriptSymbol>(),
            new ConcurrentDictionary<QualifiedName, ImmutableHashSet<QualifiedName>>(),
            new ConcurrentDictionary<QualifiedName, int>(),
            logger);
    }

    public IScriptSymbol GetScript(QualifiedName name)
    {
        name = Intern(name);
        using var activity = PapyrusActivitySource.Start(
            "bind",
            [new KeyValuePair<string, object?>("script", name.ToString())]);
        PapyrusEventSource.Log.Bind();
        _logger?.LogDebug("GetScript {Script} Activity={ActivityId}", name, Activity.Current?.Id);

        RecordUse(name);
        var script = _scripts.GetOrAdd(name, n => CreateScript(n, GetOrAddCached(n)));
        return CompleteScript(name, script, CompletionPart.All);
    }

    internal IScriptSymbol GetScriptForType(QualifiedName name)
    {
        name = Intern(name);
        RecordUse(name);
        if (!_scripts.TryGetValue(name, out var script))
        {
            if (!CanResolve(name))
            {
                return CreateErrorScript(name);
            }

            script = _scripts.GetOrAdd(name, n => CreateScript(n, GetOrAddCached(n)));
        }

        return CompleteScript(name, script, CompletionPart.Members);
    }

    public IReadOnlyCollection<QualifiedName> GetDependents(QualifiedName name)
    {
        name = Intern(name);
        if (!_dependents.TryGetValue(name, out var direct) || direct.Count == 0)
        {
            return Array.Empty<QualifiedName>();
        }

        var result = new List<QualifiedName>();
        var seen = new HashSet<QualifiedName>();
        var pending = new Stack<QualifiedName>();
        foreach (var dependent in direct)
        {
            pending.Push(dependent);
        }

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            result.Add(current);
            if (!_dependents.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var dependent in next)
            {
                pending.Push(dependent);
            }
        }

        return result;
    }

    internal TypeName GetInternedTypeName(TypeNameSyntax syntax)
    {
        var name = GetInternedQualifiedName(syntax.Name);
        PapyrusName? structName = null;
        if (syntax.HashToken.Kind == SyntaxKind.HashToken
            && !syntax.StructName.IsMissing
            && syntax.StructName.Text.Length > 0)
        {
            structName = Names.Get(syntax.StructName.Text);
        }

        var isArray = syntax.OpenBracketToken.Kind == SyntaxKind.OpenBracketToken;
        return new TypeName(name, isArray, structName);
    }

    public SyntaxTree GetSyntaxTree(QualifiedName name)
    {
        name = Intern(name);
        using var activity = PapyrusActivitySource.Start(
            "parse",
            [new KeyValuePair<string, object?>("script", name.ToString())]);
        _logger?.LogTrace("GetSyntaxTree {Script} Activity={ActivityId}", name, Activity.Current?.Id);
        return GetOrAddCached(name).Tree;
    }

    public SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return new SemanticModel(this, tree);
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

    public PapyrusCompilation WithoutScript(QualifiedName name)
    {
        name = Intern(name);
        return ReplaceTree(name, tree: null);
    }

    public ImmutableArray<Diagnostic> GetDiagnostics(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var cached in _cache.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddRange(cached.Tree.GetDiagnostics(cancellationToken));
        }

        foreach (var script in _scripts.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (script is SourceScriptSymbol source)
            {
                var binding = source.GetBindingDiagnostics();
                builder.AddRange(binding);
                PapyrusEventSource.Log.AddDiagnostics(binding);
            }
        }

        return builder.ToImmutable();
    }

    private PapyrusCompilation ReplaceTree(QualifiedName name, SyntaxTree? tree)
    {
        var invalidated = CollectInvalidated(name);
        var overrides = tree is null ? _overrides.Remove(name) : _overrides.SetItem(name, tree);

        var cache = new ConcurrentDictionary<QualifiedName, CachedScript>();
        foreach (var pair in _cache)
        {
            if (pair.Key.Equals(name))
            {
                continue;
            }

            cache.TryAdd(pair.Key, pair.Value);
        }

        if (tree is not null)
        {
            cache[name] = CachedScript.Source(tree);
        }

        var scripts = new ConcurrentDictionary<QualifiedName, IScriptSymbol>();
        foreach (var pair in _scripts)
        {
            if (invalidated.Contains(pair.Key))
            {
                continue;
            }

            scripts.TryAdd(pair.Key, pair.Value);
        }

        if (tree is not null)
        {
            scripts[name] = CreateSourceScript(name, tree);
        }

        var versions = new ConcurrentDictionary<QualifiedName, int>(_versions);
        versions.AddOrUpdate(name, 1, static (_, version) => version + 1);

        return new PapyrusCompilation(
            Dialect,
            Resolver,
            Names,
            _options,
            overrides,
            cache,
            scripts,
            CopyDependents(invalidated),
            versions,
            _logger);
    }

    private IScriptSymbol CompleteScript(QualifiedName name, IScriptSymbol script, CompletionPart part)
    {
        if (script is not SourceScriptSymbol source)
        {
            return script;
        }

        PushBinding(name);
        try
        {
            source.ForceComplete(part, CancellationToken.None);
        }
        finally
        {
            PopBinding();
        }

        return script;
    }

    private void RecordUse(QualifiedName used)
    {
        var stack = t_binding;
        if (stack is null || stack.Count == 0)
        {
            return;
        }

        var user = stack[^1];
        if (user.Equals(used))
        {
            return;
        }

        _dependents.AddOrUpdate(
            used,
            _ => ImmutableHashSet.Create(user),
            (_, existing) => existing.Add(user));
    }

    private static void PushBinding(QualifiedName name)
    {
        (t_binding ??= new List<QualifiedName>()).Add(name);
    }

    private static void PopBinding()
    {
        var stack = t_binding;
        if (stack is null || stack.Count == 0)
        {
            return;
        }

        stack.RemoveAt(stack.Count - 1);
    }

    private HashSet<QualifiedName> CollectInvalidated(QualifiedName name)
    {
        var drop = new HashSet<QualifiedName> { name };
        foreach (var dependent in GetDependents(name))
        {
            drop.Add(dependent);
        }

        return drop;
    }

    private ConcurrentDictionary<QualifiedName, ImmutableHashSet<QualifiedName>> CopyDependents(
        HashSet<QualifiedName> invalidated)
    {
        var dependents = new ConcurrentDictionary<QualifiedName, ImmutableHashSet<QualifiedName>>();
        foreach (var pair in _dependents)
        {
            if (invalidated.Contains(pair.Key))
            {
                continue;
            }

            var users = pair.Value;
            var filtered = users;
            foreach (var user in users)
            {
                if (invalidated.Contains(user))
                {
                    filtered = filtered.Remove(user);
                }
            }

            if (filtered.Count > 0)
            {
                dependents.TryAdd(pair.Key, filtered);
            }
        }

        return dependents;
    }

    private CachedScript GetOrAddCached(QualifiedName name)
    {
        if (_cache.TryGetValue(name, out var cached))
        {
            PapyrusEventSource.Log.ParseCacheHit();
            return cached;
        }

        return _cache.GetOrAdd(name, LoadCached);
    }

    private CachedScript LoadCached(QualifiedName name)
    {
        if (_overrides.TryGetValue(name, out var overrideTree))
        {
            return CachedScript.Source(overrideTree);
        }

        if (Resolver.TryResolve(name, out var text, out _))
        {
            return CachedScript.Source(SyntaxTree.Parse(text, Dialect, _options));
        }

        if (Resolver.TryResolvePex(name.ToString(), out var metadata) && metadata is not null)
        {
            return CachedScript.FromPex(
                SyntaxTree.Parse(SourceText.From(string.Empty), Dialect, _options),
                metadata);
        }

        return CachedScript.Missing(CreateUnresolvedTree(name));
    }

    private IScriptSymbol CreateScript(QualifiedName name, CachedScript cached)
    {
        return cached.Kind switch
        {
            ScriptKind.Source => CreateSourceScript(name, cached.Tree),
            ScriptKind.Pex when cached.Pex is not null
                => PexSymbolAdapter.ToScriptSymbol(cached.Pex, Names, Dialect),
            _ => CreateErrorScript(name),
        };
    }

    private SyntaxTree CreateUnresolvedTree(QualifiedName name)
    {
        var tree = SyntaxTree.Parse(SourceText.From(string.Empty), Dialect, _options);
        return tree.WithRootDiagnostics(
            new DiagnosticInfo(DiagnosticCatalog.UnresolvedScript, name));
    }

    private IScriptSymbol CreateSourceScript(QualifiedName name, SyntaxTree tree)
    {
        var references = ImmutableArray<SyntaxReference>.Empty;
        var isNative = false;
        if (tree.TryGetRoot(out var root))
        {
            var span = root.Name.IsMissing ? root.Span : root.Name.Span;
            references = ImmutableArray.Create(new SyntaxReference(tree, span));
            isNative = root.NativeKeyword.Kind == SyntaxKind.NativeKeyword;
        }

        return new SourceScriptSymbol(
            this,
            tree,
            SimpleName(name),
            name,
            new TypeName(name),
            references,
            isNative);
    }

    private static IScriptSymbol CreateErrorScript(QualifiedName name)
    {
        return new ScriptSymbol(SimpleName(name), name, new TypeName(name), isError: true);
    }

    internal QualifiedName GetInternedQualifiedName(NameSyntax name) => Intern(ToQualifiedName(name));

    private bool CanResolve(QualifiedName name)
    {
        if (_overrides.ContainsKey(name) || _cache.ContainsKey(name))
        {
            return true;
        }

        if (Resolver.TryResolve(name, out _, out _))
        {
            return true;
        }

        return Resolver.TryResolvePex(name.ToString(), out _);
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

    private enum ScriptKind
    {
        Source,
        Pex,
        Missing,
    }

    private readonly struct CachedScript
    {
        private CachedScript(ScriptKind kind, SyntaxTree tree, PexScriptMetadata? pex)
        {
            Kind = kind;
            Tree = tree;
            Pex = pex;
        }

        public ScriptKind Kind { get; }

        public SyntaxTree Tree { get; }

        public PexScriptMetadata? Pex { get; }

        public static CachedScript Source(SyntaxTree tree) => new(ScriptKind.Source, tree, pex: null);

        public static CachedScript FromPex(SyntaxTree tree, PexScriptMetadata metadata)
            => new(ScriptKind.Pex, tree, metadata);

        public static CachedScript Missing(SyntaxTree tree) => new(ScriptKind.Missing, tree, pex: null);
    }
}
