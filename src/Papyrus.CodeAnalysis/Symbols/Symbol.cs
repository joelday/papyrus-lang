using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Symbols;

internal abstract class Symbol : ISymbol
{
    private readonly IScriptSymbol? _containingScript;

    protected Symbol(
        PapyrusName name,
        SymbolKind kind,
        ISymbol? containingSymbol,
        IScriptSymbol? containingScript,
        bool isNative)
    {
        Name = name;
        Kind = kind;
        ContainingSymbol = containingSymbol;
        _containingScript = containingScript;
        IsNative = isNative;
    }

    public PapyrusName Name { get; }

    public SymbolKind Kind { get; }

    public ISymbol? ContainingSymbol { get; }

    public virtual IScriptSymbol? ContainingScript => _containingScript;

    public ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;

    public bool IsNative { get; }

    public virtual ImmutableArray<ISymbol> GetMembers() => ImmutableArray<ISymbol>.Empty;

    public virtual ImmutableArray<ISymbol> GetMembers(PapyrusName name) => ImmutableArray<ISymbol>.Empty;
}

internal abstract class MemberOwnerSymbol : Symbol
{
    private SymbolMemberTable _members = SymbolMemberTable.Empty;

    protected MemberOwnerSymbol(
        PapyrusName name,
        SymbolKind kind,
        ISymbol? containingSymbol,
        IScriptSymbol? containingScript,
        bool isNative)
        : base(name, kind, containingSymbol, containingScript, isNative)
    {
    }

    internal void SetMembers(ImmutableArray<ISymbol> members)
    {
        _members = new SymbolMemberTable(members);
    }

    public override ImmutableArray<ISymbol> GetMembers() => _members.All;

    public override ImmutableArray<ISymbol> GetMembers(PapyrusName name) => _members.Get(name);
}

internal sealed class SymbolMemberTable
{
    public static SymbolMemberTable Empty { get; } = new(ImmutableArray<ISymbol>.Empty);

    private readonly ImmutableArray<ISymbol> _all;
    private readonly FrozenDictionary<PapyrusName, ImmutableArray<ISymbol>> _byName;

    public SymbolMemberTable(ImmutableArray<ISymbol> members)
    {
        _all = members.IsDefault ? ImmutableArray<ISymbol>.Empty : members;
        if (_all.IsEmpty)
        {
            _byName = FrozenDictionary<PapyrusName, ImmutableArray<ISymbol>>.Empty;
            return;
        }

        var grouped = new Dictionary<PapyrusName, ImmutableArray<ISymbol>.Builder>();
        foreach (var member in _all)
        {
            if (!grouped.TryGetValue(member.Name, out var builder))
            {
                builder = ImmutableArray.CreateBuilder<ISymbol>();
                grouped.Add(member.Name, builder);
            }

            builder.Add(member);
        }

        _byName = grouped.ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value.ToImmutable());
    }

    public ImmutableArray<ISymbol> All => _all;

    public ImmutableArray<ISymbol> Get(PapyrusName name)
        => _byName.TryGetValue(name, out var members) ? members : ImmutableArray<ISymbol>.Empty;
}
