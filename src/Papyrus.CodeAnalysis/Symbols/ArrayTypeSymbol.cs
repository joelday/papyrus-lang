using System;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class ArrayTypeSymbol : MemberOwnerSymbol, ITypeSymbol
{
    public ArrayTypeSymbol(ITypeSymbol elementType, NameTable names, PapyrusDialect? dialect = null)
        : base(
            RequireNames(names).Get(MakeArrayTypeName(RequireElement(elementType)).ToString()),
            SymbolKind.Array,
            containingSymbol: null,
            containingScript: null,
            isNative: true)
    {
        ElementType = elementType;
        TypeName = MakeArrayTypeName(elementType);
        SetMembers(CreateMembers(this, elementType, names, dialect ?? PapyrusDialect.SkyrimSe));
    }

    public ITypeSymbol ElementType { get; }

    public TypeName TypeName { get; }

    public TypeKind TypeKind => TypeKind.Array;

    public ITypeSymbol? GetArrayElementType() => ElementType;

    private static ITypeSymbol RequireElement(ITypeSymbol elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return elementType;
    }

    private static NameTable RequireNames(NameTable names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names;
    }

    private static TypeName MakeArrayTypeName(ITypeSymbol elementType)
    {
        return new TypeName(elementType.TypeName.Name, isArray: true, elementType.TypeName.StructName);
    }

    private static ImmutableArray<ISymbol> CreateMembers(
        ArrayTypeSymbol array,
        ITypeSymbol elementType,
        NameTable names,
        PapyrusDialect dialect)
    {
        var intType = IntrinsicTypeSymbol.Create(names, "Int");
        var noneType = IntrinsicTypeSymbol.Create(names, "None");
        var stringType = IntrinsicTypeSymbol.Create(names, "String");
        var varType = IntrinsicTypeSymbol.Create(names, "Var");

        var members = ImmutableArray.CreateBuilder<ISymbol>();
        members.Add(new PropertySymbol(names.Get("Length"), array, containingScript: null, intType, isAuto: true));
        members.Add(CreateFunction(
            array,
            names.Get("Find"),
            intType,
            Parameter(names, "akElement", elementType, hasDefaultValue: false),
            Parameter(names, "aiStartIndex", intType, hasDefaultValue: true)));
        members.Add(CreateFunction(
            array,
            names.Get("RFind"),
            intType,
            Parameter(names, "akElement", elementType, hasDefaultValue: false),
            Parameter(names, "aiStartIndex", intType, hasDefaultValue: true)));

        if (dialect.SupportsFo4Plus)
        {
            members.Add(CreateFunction(
                array,
                names.Get("Add"),
                noneType,
                Parameter(names, "akElement", elementType, hasDefaultValue: false),
                Parameter(names, "aiCount", intType, hasDefaultValue: true)));
            members.Add(CreateFunction(
                array,
                names.Get("Insert"),
                noneType,
                Parameter(names, "akElement", elementType, hasDefaultValue: false),
                Parameter(names, "aiLocation", intType, hasDefaultValue: false)));
            members.Add(CreateFunction(
                array,
                names.Get("Remove"),
                noneType,
                Parameter(names, "aiLocation", intType, hasDefaultValue: false),
                Parameter(names, "aiCount", intType, hasDefaultValue: true)));
            members.Add(CreateFunction(array, names.Get("RemoveLast"), noneType));
            members.Add(CreateFunction(array, names.Get("Clear"), noneType));

            if (elementType.TypeKind == TypeKind.Struct)
            {
                members.Add(CreateFunction(
                    array,
                    names.Get("FindStruct"),
                    intType,
                    Parameter(names, "asVarName", stringType, hasDefaultValue: false),
                    Parameter(names, "akElement", varType, hasDefaultValue: false),
                    Parameter(names, "aiStartIndex", intType, hasDefaultValue: true)));
                members.Add(CreateFunction(
                    array,
                    names.Get("RFindStruct"),
                    intType,
                    Parameter(names, "asVarName", stringType, hasDefaultValue: false),
                    Parameter(names, "akElement", varType, hasDefaultValue: false),
                    Parameter(names, "aiStartIndex", intType, hasDefaultValue: true)));
            }
        }

        return members.ToImmutable();
    }

    private static (PapyrusName Name, ITypeSymbol Type, bool HasDefaultValue) Parameter(
        NameTable names,
        string name,
        ITypeSymbol type,
        bool hasDefaultValue)
    {
        return (names.Get(name), type, hasDefaultValue);
    }

    private static FunctionSymbol CreateFunction(
        ISymbol containing,
        PapyrusName name,
        ITypeSymbol returnType,
        params (PapyrusName Name, ITypeSymbol Type, bool HasDefaultValue)[] parameters)
    {
        var function = new FunctionSymbol(
            name,
            containing,
            containingScript: null,
            returnType,
            isGlobal: false,
            isNative: true);

        if (parameters.Length == 0)
        {
            return function;
        }

        var builder = ImmutableArray.CreateBuilder<IParameterSymbol>(parameters.Length);
        foreach (var parameter in parameters)
        {
            builder.Add(new ParameterSymbol(
                parameter.Name,
                function,
                containingScript: null,
                parameter.Type,
                parameter.HasDefaultValue));
        }

        function.SetParameters(builder.MoveToImmutable());
        return function;
    }
}
