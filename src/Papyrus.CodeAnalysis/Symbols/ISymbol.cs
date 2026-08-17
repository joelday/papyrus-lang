using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Symbols;

public interface ISymbol
{
    PapyrusName Name { get; }
    SymbolKind Kind { get; }
    ISymbol? ContainingSymbol { get; }
    IScriptSymbol? ContainingScript { get; }
    ImmutableArray<SyntaxReference> DeclaringSyntaxReferences { get; }
    ImmutableArray<ISymbol> GetMembers();
    ImmutableArray<ISymbol> GetMembers(PapyrusName name);
    bool IsNative { get; }
}

public interface ITypeSymbol : ISymbol
{
    TypeKind TypeKind { get; }
    TypeName TypeName { get; }
    ITypeSymbol? GetArrayElementType();
}

public interface IScriptSymbol : ITypeSymbol
{
    QualifiedName QualifiedName { get; }
    IScriptSymbol? BaseType { get; }
}

public interface IFunctionSymbol : ISymbol
{
    ITypeSymbol ReturnType { get; }
    ImmutableArray<IParameterSymbol> Parameters { get; }
    ImmutableArray<IVariableSymbol> Locals { get; }
    bool IsGlobal { get; }
}

public interface IPropertySymbol : ISymbol
{
    ITypeSymbol Type { get; }
    bool IsAuto { get; }
    IFunctionSymbol? GetMethod { get; }
    IFunctionSymbol? SetMethod { get; }
}

public interface IVariableSymbol : ISymbol
{
    ITypeSymbol Type { get; }
    bool IsConst { get; }
}

public interface IStructSymbol : ITypeSymbol
{
}

public interface IStateSymbol : ISymbol
{
    bool IsAuto { get; }
}

public interface IEventSymbol : IFunctionSymbol
{
}

public interface ICustomEventSymbol : ISymbol
{
}

public interface IGuardSymbol : ISymbol
{
}

public interface IParameterSymbol : ISymbol
{
    ITypeSymbol Type { get; }
    bool HasDefaultValue { get; }
}
