using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class ScriptSymbol : MemberOwnerSymbol, IScriptSymbol
{
    private readonly ImmutableArray<SyntaxReference> _declaringSyntaxReferences;

    public ScriptSymbol(
        PapyrusName name,
        QualifiedName qualifiedName,
        TypeName typeName,
        ImmutableArray<SyntaxReference> declaringSyntaxReferences = default,
        bool isError = false
    )
        : base(
            name,
            SymbolKind.Script,
            containingSymbol: null,
            containingScript: null,
            isNative: false
        )
    {
        QualifiedName = qualifiedName;
        TypeName = typeName;
        TypeKind = isError ? TypeKind.Error : TypeKind.Script;
        _declaringSyntaxReferences = declaringSyntaxReferences.IsDefault
            ? ImmutableArray<SyntaxReference>.Empty
            : declaringSyntaxReferences;
    }

    public override IScriptSymbol? ContainingScript => this;

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences =>
        _declaringSyntaxReferences;

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
    public StateSymbol(
        PapyrusName name,
        IScriptSymbol containingScript,
        bool isAuto,
        StateDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.State, containingScript, containingScript, isNative: false)
    {
        IsAuto = isAuto;
        Declaration = declaration;
    }

    public bool IsAuto { get; }

    internal StateDeclarationSyntax? Declaration { get; }
}

internal sealed class GuardSymbol : Symbol, IGuardSymbol
{
    public GuardSymbol(
        PapyrusName name,
        IScriptSymbol containingScript,
        GuardDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.Guard, containingScript, containingScript, isNative: false)
    {
        Declaration = declaration;
    }

    internal GuardDeclarationSyntax? Declaration { get; }
}

internal sealed class FunctionSymbol : Symbol, IFunctionSymbol
{
    private ITypeSymbol _returnType;
    private ImmutableArray<IParameterSymbol> _parameters;
    private ImmutableArray<IVariableSymbol> _locals;

    public FunctionSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol returnType,
        bool isGlobal,
        bool isNative,
        FunctionDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.Function, containingSymbol, containingScript, isNative)
    {
        _returnType = returnType;
        _parameters = ImmutableArray<IParameterSymbol>.Empty;
        _locals = ImmutableArray<IVariableSymbol>.Empty;
        IsGlobal = isGlobal;
        Declaration = declaration;
    }

    internal FunctionDeclarationSyntax? Declaration { get; }

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences =>
        Declaration is null
            ? ImmutableArray<SyntaxReference>.Empty
            : [new SyntaxReference(Declaration.SyntaxTree, Declaration.Span)];

    public ITypeSymbol ReturnType
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _returnType;
        }
        internal set => _returnType = value;
    }

    public ImmutableArray<IParameterSymbol> Parameters
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _parameters;
        }
    }

    public ImmutableArray<IVariableSymbol> Locals
    {
        get
        {
            EnsurePart(CompletionPart.MemberBodies);
            return _locals;
        }
    }

    public bool IsGlobal { get; }

    internal void SetParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        _parameters = parameters.IsDefault ? ImmutableArray<IParameterSymbol>.Empty : parameters;
    }

    internal void SetLocals(ImmutableArray<IVariableSymbol> locals)
    {
        _locals = locals.IsDefault ? ImmutableArray<IVariableSymbol>.Empty : locals;
    }
}

internal sealed class EventSymbol : Symbol, IEventSymbol
{
    private ITypeSymbol _returnType;
    private ImmutableArray<IParameterSymbol> _parameters;
    private ImmutableArray<IVariableSymbol> _locals;

    public EventSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol returnType,
        bool isNative,
        EventDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.Event, containingSymbol, containingScript, isNative)
    {
        _returnType = returnType;
        _parameters = ImmutableArray<IParameterSymbol>.Empty;
        _locals = ImmutableArray<IVariableSymbol>.Empty;
        Declaration = declaration;
    }

    internal EventDeclarationSyntax? Declaration { get; }

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences =>
        Declaration is null
            ? ImmutableArray<SyntaxReference>.Empty
            : [new SyntaxReference(Declaration.SyntaxTree, Declaration.Span)];

    public ITypeSymbol ReturnType
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _returnType;
        }
        internal set => _returnType = value;
    }

    public ImmutableArray<IParameterSymbol> Parameters
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _parameters;
        }
    }

    public ImmutableArray<IVariableSymbol> Locals
    {
        get
        {
            EnsurePart(CompletionPart.MemberBodies);
            return _locals;
        }
    }

    public bool IsGlobal => false;

    internal void SetParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        _parameters = parameters.IsDefault ? ImmutableArray<IParameterSymbol>.Empty : parameters;
    }

    internal void SetLocals(ImmutableArray<IVariableSymbol> locals)
    {
        _locals = locals.IsDefault ? ImmutableArray<IVariableSymbol>.Empty : locals;
    }
}

internal sealed class CustomEventSymbol : Symbol, ICustomEventSymbol
{
    public CustomEventSymbol(PapyrusName name, IScriptSymbol containingScript)
        : base(name, SymbolKind.CustomEvent, containingScript, containingScript, isNative: false)
    { }
}

internal sealed class PropertySymbol : Symbol, IPropertySymbol
{
    private ITypeSymbol _type;
    private IFunctionSymbol? _getMethod;
    private IFunctionSymbol? _setMethod;

    public PropertySymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool isAuto,
        PropertyDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.Property, containingSymbol, containingScript, isNative: false)
    {
        _type = type;
        IsAuto = isAuto;
        Declaration = declaration;
    }

    internal PropertyDeclarationSyntax? Declaration { get; }

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences =>
        Declaration is null
            ? ImmutableArray<SyntaxReference>.Empty
            : [new SyntaxReference(Declaration.SyntaxTree, Declaration.Span)];

    public ITypeSymbol Type
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _type;
        }
        internal set => _type = value;
    }

    public bool IsAuto { get; }

    public IFunctionSymbol? GetMethod
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _getMethod;
        }
        internal set => _getMethod = value;
    }

    public IFunctionSymbol? SetMethod
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _setMethod;
        }
        internal set => _setMethod = value;
    }
}

internal sealed class VariableSymbol : Symbol, IVariableSymbol
{
    private ITypeSymbol _type;

    public VariableSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool isConst,
        VariableDeclarationSyntax? declaration = null
    )
        : base(name, SymbolKind.Variable, containingSymbol, containingScript, isNative: false)
    {
        _type = type;
        IsConst = isConst;
        Declaration = declaration;
    }

    internal VariableDeclarationSyntax? Declaration { get; }

    /// <summary>For a function local, the statement that declares it.</summary>
    internal DeclareStatementSyntax? LocalDeclaration { get; init; }

    /// <summary>
    /// The name written to PEX. Locals declared again with the same name in another block
    /// get <c>::mangled_&lt;name&gt;_&lt;n&gt;</c> so each keeps its own slot (Caprica's
    /// CheckLocalNamesStatementVisitor).
    /// </summary>
    internal string EmittedName => _emittedName ?? Name.Text;

    internal string? _emittedName { get; init; }

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences =>
        Declaration is null
            ? ImmutableArray<SyntaxReference>.Empty
            : [new SyntaxReference(Declaration.SyntaxTree, Declaration.Span)];

    public ITypeSymbol Type
    {
        get
        {
            EnsurePart(CompletionPart.MemberSignatures);
            return _type;
        }
        internal set => _type = value;
    }

    public bool IsConst { get; }
}

internal sealed class ParameterSymbol : Symbol, IParameterSymbol
{
    public ParameterSymbol(
        PapyrusName name,
        ISymbol containingSymbol,
        IScriptSymbol? containingScript,
        ITypeSymbol type,
        bool hasDefaultValue
    )
        : base(name, SymbolKind.Parameter, containingSymbol, containingScript, isNative: false)
    {
        Type = type;
        HasDefaultValue = hasDefaultValue;
    }

    public ITypeSymbol Type { get; }

    public bool HasDefaultValue { get; }
}
