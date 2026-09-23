using System;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class PexSymbolAdapterTests
{
    [Fact]
    public void ToScriptSymbol_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PexSymbolAdapter.ToScriptSymbol(null!, new NameTable()));
        Assert.Throws<ArgumentNullException>(() => PexSymbolAdapter.ToScriptSymbol(new PexScriptMetadata(), null!));
    }

    [Fact]
    public void ToScriptSymbol_MapsNameBaseTypeFunctionsPropertiesStructsGuardsAndStates()
    {
        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildFallout4Script()),
            names,
            PapyrusDialect.Fallout4);

        Assert.Equal("Foo", script.Name.Text);
        Assert.Equal("Foo", script.QualifiedName.ToString());
        Assert.Equal(TypeKind.Script, script.TypeKind);
        Assert.Equal("Bar", script.BaseType?.Name.Text);
        Assert.Same(script, script.ContainingScript);
        Assert.Null(script.ContainingSymbol);
        Assert.Empty(script.DeclaringSyntaxReferences);
        Assert.Null(script.GetArrayElementType());

        var native = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(script, names, "NativeFn"));
        Assert.Equal(SymbolKind.Function, native.Kind);
        Assert.Equal("Int", native.ReturnType.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, native.ReturnType.TypeKind);
        Assert.True(native.IsNative);
        Assert.True(native.IsGlobal);
        Assert.Same(script, native.ContainingSymbol);
        Assert.Same(script, native.ContainingScript);
        Assert.Empty(native.DeclaringSyntaxReferences);

        var property = Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(script, names, "Prop"));
        Assert.Equal(SymbolKind.Property, property.Kind);
        Assert.Equal("Int", property.Type.Name.Text);
        Assert.True(property.IsAuto);
        Assert.Null(property.GetMethod);
        Assert.Null(property.SetMethod);

        var struc = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(script, names, "Pair"));
        Assert.Equal(SymbolKind.Struct, struc.Kind);
        Assert.Equal(TypeKind.Struct, struc.TypeKind);
        Assert.Equal("Pair", struc.TypeName.StructName?.Text);
        Assert.Equal("Foo", struc.TypeName.Name.ToString());
        var left = Assert.IsAssignableFrom<IVariableSymbol>(SingleMember(struc, names, "Left"));
        Assert.Equal("Int", left.Type.Name.Text);
        Assert.True(left.IsConst);
        Assert.Same(struc, left.ContainingSymbol);
        Assert.Same(script, left.ContainingScript);

        Assert.IsAssignableFrom<IGuardSymbol>(SingleMember(script, names, "Busy"));

        var state = Assert.IsAssignableFrom<IStateSymbol>(SingleMember(script, names, "BusyState"));
        Assert.True(state.IsAuto);
        var onBegin = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(state, names, "OnBeginState"));
        Assert.Equal("None", onBegin.ReturnType.Name.Text);
        Assert.Same(state, onBegin.ContainingSymbol);
    }

    [Fact]
    public void ToScriptSymbol_MapsSkyrimFunctionParameters()
    {
        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildSkyrimScript()),
            names);

        Assert.Equal("Built", script.Name.Text);
        Assert.Null(script.BaseType);

        var go = Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(script, names, "Go"));
        Assert.Equal("None", go.ReturnType.Name.Text);
        Assert.False(go.IsNative);
        Assert.False(go.IsGlobal);
        var parameter = Assert.Single(go.Parameters);
        Assert.Equal("value", parameter.Name.Text);
        Assert.Equal("Int", parameter.Type.Name.Text);
        Assert.False(parameter.HasDefaultValue);
        Assert.Same(go, parameter.ContainingSymbol);
        Assert.Empty(parameter.DeclaringSyntaxReferences);
    }

    [Fact]
    public void ToQualifiedName_AndToTypeName_SplitNamespaceAndStruct()
    {
        var names = new NameTable();
        _ = names.Get("A");
        var pex = PexQualifiedName.Parse("a:B:Script#Struct");

        var qualified = PexSymbolAdapter.ToQualifiedName(pex, names);
        Assert.Equal(3, qualified.Parts.Length);
        Assert.Equal("A", qualified.Parts[0].Text);
        Assert.Equal("B", qualified.Parts[1].Text);
        Assert.Equal("Script", qualified.Parts[2].Text);
        Assert.Same(names.Get("A").Text, qualified.Parts[0].Text);

        var type = PexSymbolAdapter.ToTypeName(pex, names);
        Assert.Equal(qualified, type.Name);
        Assert.Equal("Struct", type.StructName?.Text);
        Assert.False(type.IsArray);
        Assert.Same(names.Get("struct").Text, type.StructName?.Text);
    }

    [Fact]
    public void ToScriptSymbol_NamespacedName_UsesQualifiedNameParts()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "A:B:Script",
                    ParentClassName = "A:B:Parent",
                    Structs =
                    [
                        new PexStruct { Name = "Member" },
                    ],
                },
            ],
        };

        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(PexReflector.Reflect(file), names, PapyrusDialect.Fallout4);

        Assert.Equal("Script", script.Name.Text);
        Assert.Equal(3, script.QualifiedName.Parts.Length);
        Assert.Equal("A", script.QualifiedName.Parts[0].Text);
        Assert.Equal("B", script.QualifiedName.Parts[1].Text);
        Assert.Equal("Script", script.QualifiedName.Parts[2].Text);
        Assert.Equal("A:B:Parent", script.BaseType?.QualifiedName.ToString());

        var struc = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(script, names, "Member"));
        Assert.Equal("A:B:Script", struc.TypeName.Name.ToString());
        Assert.Equal("Member", struc.TypeName.StructName?.Text);
    }

    [Fact]
    public void GetMembers_IsCaseInsensitive()
    {
        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildFallout4Script()),
            names,
            PapyrusDialect.Fallout4);

        var upper = script.GetMembers(names.Get("NATIVEFN"));
        var lower = script.GetMembers(new PapyrusName("nativefn"));
        Assert.Equal(upper, lower);
        Assert.Equal("NativeFn", Assert.Single(lower).Name.Text);
        Assert.Empty(script.GetMembers(names.Get("missing")));
    }

    [Fact]
    public void Intrinsics_HaveExpectedKinds()
    {
        var names = new NameTable();
        AssertIntrinsic(names, "Int", TypeKind.Intrinsic);
        AssertIntrinsic(names, "Float", TypeKind.Intrinsic);
        AssertIntrinsic(names, "String", TypeKind.Intrinsic);
        AssertIntrinsic(names, "Bool", TypeKind.Intrinsic);
        AssertIntrinsic(names, "None", TypeKind.Intrinsic);
        AssertIntrinsic(names, "Var", TypeKind.Var);
    }

    [Fact]
    public void ArrayOfInt_HasLengthAndElementType()
    {
        var names = new NameTable();
        var intType = IntrinsicTypeSymbol.Create(names, "Int");
        var array = new ArrayTypeSymbol(intType, names);

        Assert.Equal(TypeKind.Array, array.TypeKind);
        Assert.Equal(SymbolKind.Array, array.Kind);
        Assert.Same(intType, array.GetArrayElementType());
        Assert.True(array.TypeName.IsArray);
        Assert.Equal("Int", array.TypeName.Name.ToString());

        var length = Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(array, names, "Length"));
        Assert.Equal("Int", length.Type.Name.Text);
        Assert.True(length.IsAuto);

        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "Find"));
        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "RFind"));
        Assert.Empty(array.GetMembers(names.Get("Add")));
        Assert.Empty(array.DeclaringSyntaxReferences);
    }

    [Fact]
    public void ArrayOfInt_Fo4Plus_HasMutatingMembers()
    {
        var names = new NameTable();
        var intType = IntrinsicTypeSymbol.Create(names, "Int");
        var array = new ArrayTypeSymbol(intType, names, PapyrusDialect.Fallout4);

        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "Add"));
        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "Insert"));
        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "Remove"));
        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "RemoveLast"));
        Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(array, names, "Clear"));
        Assert.Empty(array.GetMembers(names.Get("FindStruct")));
        Assert.Empty(array.GetMembers(names.Get("RFindStruct")));
    }

    [Fact]
    public void ArrayOfStruct_Fo4Plus_HasFindStructMembers()
    {
        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildFallout4Script()),
            names,
            PapyrusDialect.Fallout4);
        var pair = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(script, names, "Pair"));

        var fo4 = new ArrayTypeSymbol(pair, names, PapyrusDialect.Fallout4);
        Assert.Same(pair, fo4.GetArrayElementType());
        AssertFindStruct(Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(fo4, names, "FindStruct")));
        AssertFindStruct(Assert.IsAssignableFrom<IFunctionSymbol>(SingleMember(fo4, names, "RFindStruct")));

        var skyrim = new ArrayTypeSymbol(pair, names, PapyrusDialect.SkyrimSe);
        Assert.Empty(skyrim.GetMembers(names.Get("FindStruct")));
        Assert.Empty(skyrim.GetMembers(names.Get("RFindStruct")));
    }

    [Fact]
    public void PexReflectedSymbols_HaveEmptyDeclaringSyntaxReferences()
    {
        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildFallout4Script()),
            names,
            PapyrusDialect.Fallout4);

        foreach (var member in script.GetMembers())
        {
            Assert.Empty(member.DeclaringSyntaxReferences);
            foreach (var child in member.GetMembers())
            {
                Assert.Empty(child.DeclaringSyntaxReferences);
            }
        }
    }

    [Fact]
    public void ToScriptSymbol_FullProperty_MapsGetterAndSetter()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Value",
                            TypeName = "Int",
                            IsReadable = true,
                            IsWritable = true,
                            ReadFunction = new PexFunction
                            {
                                Name = "Get",
                                ReturnTypeName = "Int",
                            },
                            WriteFunction = new PexFunction
                            {
                                Name = "Set",
                                ReturnTypeName = "None",
                                Parameters =
                                [
                                    new PexFunctionParameter { Name = "newValue", TypeName = "Int" },
                                ],
                            },
                        },
                    ],
                },
            ],
        };

        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(PexReflector.Reflect(file), names);
        var property = Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(script, names, "Value"));
        Assert.False(property.IsAuto);
        Assert.Equal("Int", property.GetMethod?.ReturnType.Name.Text);
        Assert.Equal("None", property.SetMethod?.ReturnType.Name.Text);
        Assert.Equal("newValue", Assert.Single(property.SetMethod!.Parameters).Name.Text);
        Assert.Same(property, property.GetMethod?.ContainingSymbol);
        Assert.Empty(property.GetMethod!.DeclaringSyntaxReferences);
    }

    [Fact]
    public void ToScriptSymbol_InternsNamesThroughNameTable()
    {
        var names = new NameTable();
        _ = names.Get("foo");
        var script = PexSymbolAdapter.ToScriptSymbol(
            PexReflector.Reflect(BuildFallout4Script()),
            names,
            PapyrusDialect.Fallout4);

        Assert.Equal("foo", script.Name.Text);
        Assert.Same(names.Get("FOO").Text, script.Name.Text);
    }

    [Fact]
    public void ToScriptSymbol_ArrayPropertyType_HasElementType()
    {
        var file = new PexFile
        {
            Objects =
            [
                new PexObject
                {
                    Name = "Script",
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Values",
                            TypeName = "Int[]",
                            IsAuto = true,
                            IsReadable = true,
                        },
                    ],
                },
            ],
        };

        var names = new NameTable();
        var script = PexSymbolAdapter.ToScriptSymbol(PexReflector.Reflect(file), names);
        var property = Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(script, names, "Values"));
        var element = property.Type.GetArrayElementType();
        Assert.NotNull(element);
        Assert.Equal("Int", element.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, element.TypeKind);
        Assert.IsAssignableFrom<IPropertySymbol>(SingleMember(property.Type, names, "Length"));
    }

    private static void AssertIntrinsic(NameTable names, string name, TypeKind typeKind)
    {
        var symbol = IntrinsicTypeSymbol.Create(names, name);
        Assert.Equal(name, symbol.Name.Text);
        Assert.Equal(SymbolKind.Intrinsic, symbol.Kind);
        Assert.Equal(typeKind, symbol.TypeKind);
        Assert.True(symbol.IsNative);
        Assert.Null(symbol.GetArrayElementType());
        Assert.Empty(symbol.DeclaringSyntaxReferences);
        Assert.Empty(symbol.GetMembers());
    }

    private static ISymbol SingleMember(ISymbol container, NameTable names, string name)
    {
        return Assert.Single(container.GetMembers(names.Get(name)));
    }

    private static void AssertFindStruct(IFunctionSymbol function)
    {
        Assert.Equal("Int", function.ReturnType.Name.Text);
        Assert.True(function.IsNative);
        Assert.Equal(3, function.Parameters.Length);
        Assert.Equal("asVarName", function.Parameters[0].Name.Text);
        Assert.Equal("String", function.Parameters[0].Type.Name.Text);
        Assert.False(function.Parameters[0].HasDefaultValue);
        Assert.Equal("akElement", function.Parameters[1].Name.Text);
        Assert.Equal("Var", function.Parameters[1].Type.Name.Text);
        Assert.Equal(TypeKind.Var, function.Parameters[1].Type.TypeKind);
        Assert.False(function.Parameters[1].HasDefaultValue);
        Assert.Equal("aiStartIndex", function.Parameters[2].Name.Text);
        Assert.Equal("Int", function.Parameters[2].Type.Name.Text);
        Assert.True(function.Parameters[2].HasDefaultValue);
    }

    private static PexFile BuildSkyrimScript()
    {
        return new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            UserFlags =
            [
                new PexUserFlag { Name = "Hidden", BitIndex = 0 },
                new PexUserFlag { Name = "Conditional", BitIndex = 1 },
            ],
            Objects =
            [
                new PexObject
                {
                    Name = "Built",
                    ParentClassName = "",
                    UserFlags = 1,
                    AutoStateName = "",
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "Go",
                                    ReturnTypeName = "None",
                                    UserFlags = 1,
                                    Parameters =
                                    [
                                        new PexFunctionParameter { Name = "value", TypeName = "Int" },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }

    private static PexFile BuildFallout4Script()
    {
        return new PexFile
        {
            Version = PexVersion.V39,
            RawGameId = GameIdMapping.Fallout4Raw,
            Game = GameId.Fallout4,
            Objects =
            [
                new PexObject
                {
                    Name = "Foo",
                    ParentClassName = "Bar",
                    AutoStateName = "BusyState",
                    Structs =
                    [
                        new PexStruct
                        {
                            Name = "Pair",
                            Members =
                            [
                                new PexStructMember
                                {
                                    Name = "Left",
                                    TypeName = "Int",
                                    UserFlags = 2,
                                    IsConst = true,
                                },
                            ],
                        },
                    ],
                    Guards =
                    [
                        new PexGuard { Name = "Busy" },
                    ],
                    Properties =
                    [
                        new PexProperty
                        {
                            Name = "Prop",
                            TypeName = "Int",
                            IsAuto = true,
                            IsReadable = true,
                            IsWritable = true,
                            AutoVar = "::Prop",
                        },
                    ],
                    States =
                    [
                        new PexState
                        {
                            Name = "",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "NativeFn",
                                    ReturnTypeName = "Int",
                                    IsNative = true,
                                    IsGlobal = true,
                                },
                            ],
                        },
                        new PexState
                        {
                            Name = "BusyState",
                            Functions =
                            [
                                new PexFunction
                                {
                                    Name = "OnBeginState",
                                    ReturnTypeName = "None",
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
