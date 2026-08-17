using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class ScriptSymbol : MemberOwnerSymbol, IScriptSymbol
{
    private readonly ImmutableArray<SyntaxReference> _declaringSyntaxReferences;

    public ScriptSymbol(
        PapyrusName name,
        QualifiedName qualifiedName,
        TypeName typeName,
        ImmutableArray<SyntaxReference> declaringSyntaxReferences = default,
        bool isError = false)
        : base(name, SymbolKind.Script, containingSymbol: null, containingScript: null, isNative: false)
    {
        QualifiedName = qualifiedName;
        TypeName = typeName;
        TypeKind = isError ? TypeKind.Error : TypeKind.Script;
        _declaringSyntaxReferences = declaringSyntaxReferences.IsDefault
            ? ImmutableArray<SyntaxReference>.Empty
            : declaringSyntaxReferences;
    }

    public override IScriptSymbol? ContainingScript => this;

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => _declaringSyntaxReferences;

    public QualifiedName QualifiedName { get; }

    public TypeName TypeName { get; }

    public TypeKind TypeKind { get; }

    internal bool IsError => TypeKind == TypeKind.Error;

    public IScriptSymbol? BaseType { get; internal set; }

    public ITypeSymbol? GetArrayElementType() => null;
}

internal sealed class StructSymbol : MemberOwnerSymbol, IStructSymbol
{
    public StructSymbol(PapyrusName name, IScriptSymbol containingScript, TypeName typeName)
        : base(name, SymbolKind.Struct, containingScript, containingScript, isNative: false)
    {
        TypeName = typeName;
    }

    public TypeName TypeName { get; }

    public TypeKind TypeKind => TypeKind.Struct;

    public ITypeSymbol? GetArrayElementType() => null;
}

internal sealed class StateSymbol : MemberOwnerSymbol, IStateSymbol
{
    public StateSymbol(PapyrusName name, IScriptSymbol containingScript, bool isAuto)
        : base(name, SymbolKind.State, containingScript, containingScript, isNative: false)
    {
        IsAuto = isAuto;
    }

    public bool IsAuto { get; }
}

internal sealed class GuardSymbol : Symbol, IGuardSymbol
{
    public GuardSymbol(PapyrusName name, IScriptSymbol containingScript)
        : base(name, SymbolKind.Guard, containingScript, containingScript, isNative: false)
    {
    }
}

internal sealed class FunctionSymbol : Symbol, IFunctionSymbol
{
    public FunctionSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol returnType,
        bool isGlobal,
        bool isNative)
        : base(name, SymbolKind.Function, containingSymbol, containingScript, isNative)
    {
        ReturnType = returnType;
        Parameters = ImmutableArray<IParameterSymbol>.Empty;
        IsGlobal = isGlobal;
    }

    public ITypeSymbol ReturnType { get; }

    public ImmutableArray<IParameterSymbol> Parameters { get; private set; }

    public bool IsGlobal { get; }

    internal void SetParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        Parameters = parameters.IsDefault ? ImmutableArray<IParameterSymbol>.Empty : parameters;
    }
}

internal sealed class EventSymbol : Symbol, IEventSymbol
{
    public EventSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol returnType,
        bool isNative)
        : base(name, SymbolKind.Event, containingSymbol, containingScript, isNative)
    {
        ReturnType = returnType;
        Parameters = ImmutableArray<IParameterSymbol>.Empty;
    }

    public ITypeSymbol ReturnType { get; }

    public ImmutableArray<IParameterSymbol> Parameters { get; private set; }

    public bool IsGlobal => false;

    internal void SetParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        Parameters = parameters.IsDefault ? ImmutableArray<IParameterSymbol>.Empty : parameters;
    }
}

internal sealed class CustomEventSymbol : Symbol, ICustomEventSymbol
{
    public CustomEventSymbol(PapyrusName name, IScriptSymbol containingScript)
        : base(name, SymbolKind.CustomEvent, containingScript, containingScript, isNative: false)
    {
    }
}

internal sealed class PropertySymbol : Symbol, IPropertySymbol
{
    public PropertySymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool isAuto)
        : base(name, SymbolKind.Property, containingSymbol, containingScript, isNative: false)
    {
        Type = type;
        IsAuto = isAuto;
    }

    public ITypeSymbol Type { get; }

    public bool IsAuto { get; }

    public IFunctionSymbol? GetMethod { get; internal set; }

    public IFunctionSymbol? SetMethod { get; internal set; }
}

internal sealed class VariableSymbol : Symbol, IVariableSymbol
{
    public VariableSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool isConst)
        : base(name, SymbolKind.Variable, containingSymbol, containingScript, isNative: false)
    {
        Type = type;
        IsConst = isConst;
    }

    public ITypeSymbol Type { get; }

    public bool IsConst { get; }
}

internal sealed class ParameterSymbol : Symbol, IParameterSymbol
{
    public ParameterSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool hasDefaultValue)
        : base(name, SymbolKind.Parameter, containingSymbol, containingScript, isNative: false)
    {
        Type = type;
        HasDefaultValue = hasDefaultValue;
    }

    public ITypeSymbol Type { get; }

    public bool HasDefaultValue { get; }
}
