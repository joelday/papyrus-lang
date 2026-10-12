using System;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class IntrinsicTypeSymbol : Symbol, ITypeSymbol
{
    public static IntrinsicTypeSymbol Create(NameTable names, string name)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(name);

        var interned = names.Get(name);
        var typeKind = interned.Equals(names.Get("Var")) ? TypeKind.Var : TypeKind.Intrinsic;
        var typeName = new TypeName(new QualifiedName(interned));
        return new IntrinsicTypeSymbol(interned, typeName, typeKind);
    }

    private IntrinsicTypeSymbol(PapyrusName name, TypeName typeName, TypeKind typeKind)
        : base(name, SymbolKind.Intrinsic, containingSymbol: null, containingScript: null, isNative: true)
    {
        TypeName = typeName;
        TypeKind = typeKind;
    }

    public TypeName TypeName { get; }

    public TypeKind TypeKind { get; }

    public ITypeSymbol? GetArrayElementType() => null;
}
