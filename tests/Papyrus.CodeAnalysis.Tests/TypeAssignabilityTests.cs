using System;
using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class TypeAssignabilityTests
{
    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void Classify_matches_conversion_matrix(string source, string destination, ConversionKind expected)
    {
        var types = CreateTypes();
        var actual = TypeAssignability.Classify(types[source], types[destination]);
        Assert.Equal(expected, actual);
        Assert.Equal(
            expected is ConversionKind.Identity or ConversionKind.Implicit,
            TypeAssignability.IsAssignable(types[source], types[destination]));
    }

    public static TheoryData<string, string, ConversionKind> MatrixCases()
    {
        var data = new TheoryData<string, string, ConversionKind>();

        AddRow(data, "Bool", "Bool", ConversionKind.Identity);
        AddRow(data, "Bool", "Int", ConversionKind.None);
        AddRow(data, "Bool", "Float", ConversionKind.None);
        AddRow(data, "Bool", "String", ConversionKind.Explicit);
        AddRow(data, "Bool", "None", ConversionKind.None);
        AddRow(data, "Bool", "Parent", ConversionKind.None);
        AddRow(data, "Bool", "Point", ConversionKind.None);
        AddRow(data, "Bool", "Int[]", ConversionKind.None);
        AddRow(data, "Bool", "Var", ConversionKind.Implicit);

        AddRow(data, "Int", "Bool", ConversionKind.Implicit);
        AddRow(data, "Int", "Int", ConversionKind.Identity);
        AddRow(data, "Int", "Float", ConversionKind.Implicit);
        AddRow(data, "Int", "String", ConversionKind.Explicit);
        AddRow(data, "Int", "None", ConversionKind.None);
        AddRow(data, "Int", "Parent", ConversionKind.None);
        AddRow(data, "Int", "Point", ConversionKind.None);
        AddRow(data, "Int", "Int[]", ConversionKind.None);
        AddRow(data, "Int", "Var", ConversionKind.Implicit);

        AddRow(data, "Float", "Bool", ConversionKind.Implicit);
        AddRow(data, "Float", "Int", ConversionKind.Explicit);
        AddRow(data, "Float", "Float", ConversionKind.Identity);
        AddRow(data, "Float", "String", ConversionKind.Explicit);
        AddRow(data, "Float", "None", ConversionKind.None);
        AddRow(data, "Float", "Parent", ConversionKind.None);
        AddRow(data, "Float", "Point", ConversionKind.None);
        AddRow(data, "Float", "Int[]", ConversionKind.None);
        AddRow(data, "Float", "Var", ConversionKind.Implicit);

        AddRow(data, "String", "Bool", ConversionKind.Implicit);
        AddRow(data, "String", "Int", ConversionKind.None);
        AddRow(data, "String", "Float", ConversionKind.None);
        AddRow(data, "String", "String", ConversionKind.Identity);
        AddRow(data, "String", "None", ConversionKind.None);
        AddRow(data, "String", "Parent", ConversionKind.None);
        AddRow(data, "String", "Point", ConversionKind.None);
        AddRow(data, "String", "Int[]", ConversionKind.None);
        AddRow(data, "String", "Var", ConversionKind.Implicit);

        AddRow(data, "None", "Bool", ConversionKind.None);
        AddRow(data, "None", "Int", ConversionKind.None);
        AddRow(data, "None", "Float", ConversionKind.None);
        AddRow(data, "None", "String", ConversionKind.None);
        AddRow(data, "None", "None", ConversionKind.Identity);
        AddRow(data, "None", "Parent", ConversionKind.None);
        AddRow(data, "None", "Point", ConversionKind.None);
        AddRow(data, "None", "Int[]", ConversionKind.None);
        AddRow(data, "None", "Var", ConversionKind.None);

        AddRow(data, "Parent", "Bool", ConversionKind.None);
        AddRow(data, "Parent", "Int", ConversionKind.None);
        AddRow(data, "Parent", "Float", ConversionKind.None);
        AddRow(data, "Parent", "String", ConversionKind.Explicit);
        AddRow(data, "Parent", "None", ConversionKind.Implicit);
        AddRow(data, "Parent", "Parent", ConversionKind.Identity);
        AddRow(data, "Parent", "Point", ConversionKind.None);
        AddRow(data, "Parent", "Int[]", ConversionKind.None);
        AddRow(data, "Parent", "Var", ConversionKind.Implicit);

        AddRow(data, "Point", "Bool", ConversionKind.None);
        AddRow(data, "Point", "Int", ConversionKind.None);
        AddRow(data, "Point", "Float", ConversionKind.None);
        AddRow(data, "Point", "String", ConversionKind.None);
        AddRow(data, "Point", "None", ConversionKind.Implicit);
        AddRow(data, "Point", "Parent", ConversionKind.None);
        AddRow(data, "Point", "Point", ConversionKind.Identity);
        AddRow(data, "Point", "Int[]", ConversionKind.None);
        AddRow(data, "Point", "Var", ConversionKind.Implicit);

        AddRow(data, "Int[]", "Bool", ConversionKind.None);
        AddRow(data, "Int[]", "Int", ConversionKind.None);
        AddRow(data, "Int[]", "Float", ConversionKind.None);
        AddRow(data, "Int[]", "String", ConversionKind.None);
        AddRow(data, "Int[]", "None", ConversionKind.Implicit);
        AddRow(data, "Int[]", "Parent", ConversionKind.None);
        AddRow(data, "Int[]", "Point", ConversionKind.None);
        AddRow(data, "Int[]", "Int[]", ConversionKind.Identity);
        AddRow(data, "Int[]", "Var", ConversionKind.Implicit);

        AddRow(data, "Var", "Bool", ConversionKind.Explicit);
        AddRow(data, "Var", "Int", ConversionKind.Explicit);
        AddRow(data, "Var", "Float", ConversionKind.Explicit);
        AddRow(data, "Var", "String", ConversionKind.Explicit);
        AddRow(data, "Var", "None", ConversionKind.Implicit);
        AddRow(data, "Var", "Parent", ConversionKind.Explicit);
        AddRow(data, "Var", "Point", ConversionKind.Explicit);
        AddRow(data, "Var", "Int[]", ConversionKind.Explicit);
        AddRow(data, "Var", "Var", ConversionKind.Identity);

        return data;
    }

    [Fact]
    public void Child_is_implicitly_assignable_to_parent_and_grandparent()
    {
        var types = CreateTypes();

        Assert.Equal(ConversionKind.Implicit, TypeAssignability.Classify(types["Child"], types["Parent"]));
        Assert.True(TypeAssignability.IsAssignable(types["Child"], types["Parent"]));
        Assert.Equal(ConversionKind.Implicit, TypeAssignability.Classify(types["Child"], types["Object"]));
        Assert.True(TypeAssignability.IsAssignable(types["Child"], types["Object"]));
        Assert.Equal(ConversionKind.Implicit, TypeAssignability.Classify(types["Parent"], types["Object"]));
    }

    [Fact]
    public void Parent_is_not_assignable_to_child()
    {
        var types = CreateTypes();

        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Parent"], types["Child"]));
        Assert.False(TypeAssignability.IsAssignable(types["Parent"], types["Child"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Object"], types["Child"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Parent"], types["Other"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Child"], types["Other"]));
    }

    [Fact]
    public void Arrays_are_not_covariant()
    {
        var types = CreateTypes();

        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Int[]"], types["Var[]"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Var[]"], types["Int[]"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Child[]"], types["Parent[]"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Parent[]"], types["Child[]"]));
        Assert.False(TypeAssignability.IsAssignable(types["Int[]"], types["Var[]"]));
        Assert.False(TypeAssignability.IsAssignable(types["Child[]"], types["Parent[]"]));
        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(types["Child[]"], types["Child[]"]));
        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(types["Parent[]"], types["Parent[]"]));
    }

    [Fact]
    public void Structs_match_exactly()
    {
        var types = CreateTypes();

        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(types["Point"], types["Point"]));
        Assert.True(TypeAssignability.IsAssignable(types["Point"], types["Point"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Point"], types["Size"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Point"], types["OtherPoint"]));
        Assert.False(TypeAssignability.IsAssignable(types["Point"], types["OtherPoint"]));
        Assert.Equal(ConversionKind.None, TypeAssignability.Classify(types["Point[]"], types["OtherPoint[]"]));
        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(types["Point[]"], types["Point[]"]));
    }

    [Fact]
    public void Error_types_are_assignable_to_and_from_anything()
    {
        var types = CreateTypes();
        var error = types["Error"];

        foreach (var destination in types.Values)
        {
            Assert.NotEqual(ConversionKind.None, TypeAssignability.Classify(error, destination));
            Assert.True(TypeAssignability.IsAssignable(error, destination));
            Assert.NotEqual(ConversionKind.None, TypeAssignability.Classify(destination, error));
            Assert.True(TypeAssignability.IsAssignable(destination, error));
        }

        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(error, types["OtherError"]));
        Assert.True(TypeAssignability.IsAssignable(types["Error[]"], types["Int[]"]));
        Assert.True(TypeAssignability.IsAssignable(types["Int[]"], types["Error[]"]));
    }

    [Fact]
    public void Separate_intrinsic_instances_compare_by_name()
    {
        var left = IntrinsicTypeSymbol.Create(new NameTable(), "Int");
        var right = IntrinsicTypeSymbol.Create(new NameTable(), "int");

        Assert.Equal(ConversionKind.Identity, TypeAssignability.Classify(left, right));
        Assert.True(TypeAssignability.IsAssignable(left, right));
    }

    [Fact]
    public void Classify_rejects_null_arguments()
    {
        var intType = IntrinsicTypeSymbol.Create(new NameTable(), "Int");
        Assert.Throws<ArgumentNullException>(() => TypeAssignability.Classify(null!, intType));
        Assert.Throws<ArgumentNullException>(() => TypeAssignability.Classify(intType, null!));
        Assert.Throws<ArgumentNullException>(() => TypeAssignability.IsAssignable(null!, intType));
        Assert.Throws<ArgumentNullException>(() => TypeAssignability.IsAssignable(intType, null!));
    }

    private static void AddRow(
        TheoryData<string, string, ConversionKind> data,
        string source,
        string destination,
        ConversionKind expected)
    {
        data.Add(source, destination, expected);
    }

    private static Dictionary<string, ITypeSymbol> CreateTypes()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Object")] = SourceText.From("ScriptName Object\n"),
                [QualifiedName.Parse("Parent")] = SourceText.From(
                    "ScriptName Parent Extends Object\n" +
                    "Struct Point\nFloat X\nEndStruct\n" +
                    "Struct Size\nFloat W\nEndStruct\n"),
                [QualifiedName.Parse("Child")] = SourceText.From("ScriptName Child Extends Parent\n"),
                [QualifiedName.Parse("Other")] = SourceText.From(
                    "ScriptName Other\nStruct Point\nFloat X\nEndStruct\n"),
            }));

        var names = compilation.Names;
        var objectType = compilation.GetScript(QualifiedName.Parse("Object"));
        var parent = compilation.GetScript(QualifiedName.Parse("Parent"));
        var child = compilation.GetScript(QualifiedName.Parse("Child"));
        var other = compilation.GetScript(QualifiedName.Parse("Other"));
        var error = compilation.GetScript(QualifiedName.Parse("Missing"));
        var otherError = compilation.GetScript(QualifiedName.Parse("NoSuch"));
        var point = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(parent, names, "Point"));
        var size = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(parent, names, "Size"));
        var otherPoint = Assert.IsAssignableFrom<IStructSymbol>(SingleMember(other, names, "Point"));
        var boolean = IntrinsicTypeSymbol.Create(names, "Bool");
        var integer = IntrinsicTypeSymbol.Create(names, "Int");
        var floating = IntrinsicTypeSymbol.Create(names, "Float");
        var text = IntrinsicTypeSymbol.Create(names, "String");
        var none = IntrinsicTypeSymbol.Create(names, "None");
        var varType = IntrinsicTypeSymbol.Create(names, "Var");

        return new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal)
        {
            ["Bool"] = boolean,
            ["Int"] = integer,
            ["Float"] = floating,
            ["String"] = text,
            ["None"] = none,
            ["Var"] = varType,
            ["Object"] = objectType,
            ["Parent"] = parent,
            ["Child"] = child,
            ["Other"] = other,
            ["Point"] = point,
            ["Size"] = size,
            ["OtherPoint"] = otherPoint,
            ["Int[]"] = new ArrayTypeSymbol(integer, names),
            ["Var[]"] = new ArrayTypeSymbol(varType, names),
            ["Parent[]"] = new ArrayTypeSymbol(parent, names),
            ["Child[]"] = new ArrayTypeSymbol(child, names),
            ["Point[]"] = new ArrayTypeSymbol(point, names),
            ["OtherPoint[]"] = new ArrayTypeSymbol(otherPoint, names),
            ["Error"] = error,
            ["OtherError"] = otherError,
            ["Error[]"] = new ArrayTypeSymbol(error, names),
        };
    }

    private static ISymbol SingleMember(ISymbol container, NameTable names, string name)
    {
        return Assert.Single(container.GetMembers(names.Get(name)));
    }
}
