using System;
using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class TypeConversionTests
{
    [Fact]
    public void Implicit_int_to_float_and_bool()
    {
        var types = CreateTypes();

        var toFloat = TypeConversions.ClassifyImplicit(types["Int"], types["Float"]);
        Assert.Equal(ConversionKind.Implicit, toFloat.Kind);
        Assert.Same(types["Float"], toFloat.ResultType);
        Assert.Null(toFloat.Diagnostic);
        Assert.True(TypeConversions.TryImplicit(types["Int"], types["Float"]));

        var toBool = TypeConversions.ClassifyImplicit(types["Int"], types["Bool"]);
        Assert.Equal(ConversionKind.Implicit, toBool.Kind);
        Assert.Same(types["Bool"], toBool.ResultType);
        Assert.True(TypeConversions.TryImplicit(types["Int"], types["Bool"]));
    }

    [Fact]
    public void Explicit_float_to_int_and_script_to_string()
    {
        var types = CreateTypes();
        var names = types.Names;

        var floatToInt = TypeConversions.ClassifyAs(types["Float"], types["Int"], names);
        Assert.Equal(ConversionKind.Explicit, floatToInt.Kind);
        Assert.Same(types["Int"], floatToInt.ResultType);
        Assert.Null(floatToInt.Diagnostic);
        Assert.False(TypeConversions.TryImplicit(types["Float"], types["Int"]));

        var scriptToString = TypeConversions.ClassifyAs(types["Parent"], types["String"], names);
        Assert.Equal(ConversionKind.Explicit, scriptToString.Kind);
        Assert.Same(types["String"], scriptToString.ResultType);
        Assert.Null(scriptToString.Diagnostic);
        Assert.False(TypeConversions.TryImplicit(types["Parent"], types["String"]));
    }

    [Fact]
    public void Child_is_assignable_to_parent_but_parent_is_not_implicit_to_child()
    {
        var types = CreateTypes();

        var upcast = TypeConversions.ClassifyImplicit(types["Child"], types["Parent"]);
        Assert.Equal(ConversionKind.Implicit, upcast.Kind);
        Assert.True(TypeConversions.TryImplicit(types["Child"], types["Parent"]));

        var downcast = TypeConversions.ClassifyImplicit(types["Parent"], types["Child"]);
        Assert.Equal(ConversionKind.None, downcast.Kind);
        Assert.False(TypeConversions.TryImplicit(types["Parent"], types["Child"]));
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, downcast.Diagnostic?.Id);
    }

    [Fact]
    public void As_parent_on_child_is_identity_or_implicit_as_unrelated_yields_none()
    {
        var types = CreateTypes();
        var names = types.Names;

        var asParent = TypeConversions.ClassifyAs(types["Child"], types["Parent"], names);
        Assert.Equal(ConversionKind.Implicit, asParent.Kind);
        Assert.Same(types["Parent"], asParent.ResultType);
        Assert.Null(asParent.Diagnostic);

        var asSelf = TypeConversions.ClassifyAs(types["Child"], types["Child"], names);
        Assert.Equal(ConversionKind.Identity, asSelf.Kind);
        Assert.Same(types["Child"], asSelf.ResultType);

        var asUnrelated = TypeConversions.ClassifyAs(types["Child"], types["Other"], names);
        Assert.Equal(ConversionKind.None, asUnrelated.Kind);
        Assert.Equal(TypeKind.Intrinsic, asUnrelated.ResultType.TypeKind);
        Assert.Equal("None", asUnrelated.ResultType.Name.Text);
        Assert.Null(asUnrelated.Diagnostic);
    }

    [Theory]
    [InlineData("Fallout4")]
    [InlineData("Starfield")]
    public void Is_on_fo4_plus_and_starfield_is_bool(string dialectName)
    {
        var types = CreateTypes();
        var dialect = dialectName == "Starfield" ? PapyrusDialect.Starfield : PapyrusDialect.Fallout4;

        var result = TypeConversions.ClassifyIs(types["Child"], types["Parent"], dialect, types.Names);
        Assert.Equal(ConversionKind.Identity, result.Kind);
        Assert.Equal("Bool", result.ResultType.Name.Text);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Is_on_skyrim_is_a_diagnostic()
    {
        var types = CreateTypes();

        var result = TypeConversions.ClassifyIs(
            types["Child"],
            types["Parent"],
            PapyrusDialect.SkyrimSe,
            types.Names);

        Assert.Equal(ConversionKind.None, result.Kind);
        Assert.Equal("Bool", result.ResultType.Name.Text);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, result.Diagnostic.Id);
        Assert.Same(DiagnosticCatalog.InvalidIsOperator, result.Diagnostic.Descriptor);
    }

    [Fact]
    public void Int_array_is_not_assignable_to_var_array_or_parent_array()
    {
        var types = CreateTypes();

        var toVarArray = TypeConversions.ClassifyImplicit(types["Int[]"], types["Var[]"]);
        Assert.Equal(ConversionKind.None, toVarArray.Kind);
        Assert.False(TypeConversions.TryImplicit(types["Int[]"], types["Var[]"]));
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, toVarArray.Diagnostic?.Id);

        var toParentArray = TypeConversions.ClassifyImplicit(types["Int[]"], types["Parent[]"]);
        Assert.Equal(ConversionKind.None, toParentArray.Kind);
        Assert.False(TypeConversions.TryImplicit(types["Int[]"], types["Parent[]"]));

        var toChildArray = TypeConversions.ClassifyImplicit(types["Child[]"], types["Parent[]"]);
        Assert.Equal(ConversionKind.None, toChildArray.Kind);
        Assert.False(TypeConversions.TryImplicit(types["Child[]"], types["Parent[]"]));
    }

    [Fact]
    public void Var_boxes_int_implicitly_and_unboxes_explicitly()
    {
        var types = CreateTypes();

        var box = TypeConversions.ClassifyImplicit(types["Int"], types["Var"]);
        Assert.Equal(ConversionKind.Implicit, box.Kind);
        Assert.True(TypeConversions.TryImplicit(types["Int"], types["Var"]));

        var implicitUnbox = TypeConversions.ClassifyImplicit(types["Var"], types["Int"]);
        Assert.Equal(ConversionKind.None, implicitUnbox.Kind);
        Assert.False(TypeConversions.TryImplicit(types["Var"], types["Int"]));
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, implicitUnbox.Diagnostic?.Id);

        var explicitUnbox = TypeConversions.ClassifyAs(types["Var"], types["Int"], types.Names);
        Assert.Equal(ConversionKind.Explicit, explicitUnbox.Kind);
        Assert.Same(types["Int"], explicitUnbox.ResultType);
        Assert.Null(explicitUnbox.Diagnostic);
    }

    [Fact]
    public void None_literal_is_implicit_to_script_none_typed_value_is_not()
    {
        var types = CreateTypes();

        var literal = TypeConversions.ClassifyImplicit(types["None"], types["Parent"], sourceIsNoneLiteral: true);
        Assert.Equal(ConversionKind.Implicit, literal.Kind);
        Assert.True(TypeConversions.TryImplicit(types["None"], types["Parent"], sourceIsNoneLiteral: true));

        var value = TypeConversions.ClassifyImplicit(types["None"], types["Parent"]);
        Assert.Equal(ConversionKind.None, value.Kind);
        Assert.False(TypeConversions.TryImplicit(types["None"], types["Parent"]));
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, value.Diagnostic?.Id);

        Assert.True(TypeConversions.TryImplicit(types["None"], types["Point"], sourceIsNoneLiteral: true));
        Assert.True(TypeConversions.TryImplicit(types["None"], types["Int[]"], sourceIsNoneLiteral: true));
        Assert.True(TypeConversions.TryImplicit(types["None"], types["Var"], sourceIsNoneLiteral: true));
        Assert.False(TypeConversions.TryImplicit(types["None"], types["Point"]));
        Assert.False(TypeConversions.TryImplicit(types["None"], types["Int[]"]));
        Assert.False(TypeConversions.TryImplicit(types["None"], types["Var"]));
    }

    [Fact]
    public void Element_assignment_uses_assignability_not_array_covariance()
    {
        var types = CreateTypes();

        var intElement = TypeConversions.ClassifyElementAssignment(types["Int"], types["Int[]"]);
        Assert.Equal(ConversionKind.Identity, intElement.Kind);
        Assert.Null(intElement.Diagnostic);

        var floatToInt = TypeConversions.ClassifyElementAssignment(types["Float"], types["Int[]"]);
        Assert.Equal(ConversionKind.None, floatToInt.Kind);
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, floatToInt.Diagnostic?.Id);

        var childToParent = TypeConversions.ClassifyElementAssignment(types["Child"], types["Parent[]"]);
        Assert.Equal(ConversionKind.Implicit, childToParent.Kind);
        Assert.Null(childToParent.Diagnostic);

        var intToVarElement = TypeConversions.ClassifyElementAssignment(types["Int"], types["Var[]"]);
        Assert.Equal(ConversionKind.Implicit, intToVarElement.Kind);
    }

    [Fact]
    public void Bind_implicit_int_as_float_and_explicit_float_as_int()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Float a = 1 as Float\n  Int b = 2.5 as Int\nEndFunction\n");
        var binder = new ExpressionTypeBinder(compilation);

        var asFloat = binder.Bind(Initializer(compilation, "Foo", 0));
        Assert.Equal(ConversionKind.Implicit, asFloat.Kind);
        Assert.Equal("Float", asFloat.ResultType.Name.Text);
        Assert.Null(asFloat.Diagnostic);

        var asInt = binder.Bind(Initializer(compilation, "Foo", 1));
        Assert.Equal(ConversionKind.Explicit, asInt.Kind);
        Assert.Equal("Int", asInt.ResultType.Name.Text);
        Assert.Null(asInt.Diagnostic);
    }

    [Fact]
    public void Bind_as_unrelated_script_yields_none()
    {
        var types = CreateTypes();
        var asUnrelated = TypeConversions.ClassifyAs(types["Child"], types["Other"], types.Names);

        Assert.Equal(ConversionKind.None, asUnrelated.Kind);
        Assert.Equal("None", asUnrelated.ResultType.Name.Text);
        Assert.Null(asUnrelated.Diagnostic);
    }

    [Fact]
    public void Bind_is_on_fallout4_is_bool()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\n  Bool x = None is Parent\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var binder = new ExpressionTypeBinder(compilation, compilation.GetScript(QualifiedName.Parse("Child")));

        var result = binder.Bind(Initializer(compilation, "Child"));
        Assert.Equal(ConversionKind.Identity, result.Kind);
        Assert.Equal("Bool", result.ResultType.Name.Text);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_is_on_starfield_is_bool()
    {
        var compilation = Compilation(
            PapyrusDialect.Starfield,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\n  Bool x = None is Parent\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var binder = new ExpressionTypeBinder(compilation, compilation.GetScript(QualifiedName.Parse("Child")));

        var result = binder.Bind(Initializer(compilation, "Child"));
        Assert.Equal("Bool", result.ResultType.Name.Text);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_is_on_skyrim_reports_pap1004()
    {
        var types = CreateTypes();
        var result = TypeConversions.ClassifyIs(
            types["Child"],
            types["Parent"],
            PapyrusDialect.SkyrimSe,
            types.Names);
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, result.Diagnostic?.Id);
        Assert.Equal("Bool", result.ResultType.Name.Text);

        // `is` is not a Skyrim keyword, so parse on FO4 and bind with a Skyrim compilation.
        var parsed = Compilation(
            PapyrusDialect.Fallout4,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\n  Bool x = None is Parent\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var skyrim = Compilation(
            PapyrusDialect.SkyrimSe,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var bound = new ExpressionTypeBinder(skyrim, skyrim.GetScript(QualifiedName.Parse("Child")))
            .Bind(Initializer(parsed, "Child"));
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, bound.Diagnostic?.Id);
        Assert.Equal("Bool", bound.ResultType.Name.Text);
        Assert.Same(parsed.GetSyntaxTree(QualifiedName.Parse("Child")), bound.Diagnostic?.Tree);
    }

    [Fact]
    public void Bind_new_array_result_is_array_of_element_type()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Int[] items = new Int[5]\nEndFunction\n");
        var binder = new ExpressionTypeBinder(compilation);

        var result = binder.Bind(Initializer(compilation, "Foo"));
        Assert.Equal(ConversionKind.Identity, result.Kind);
        Assert.Equal(TypeKind.Array, result.ResultType.TypeKind);
        Assert.Equal("Int", result.ResultType.GetArrayElementType()?.Name.Text);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_none_literal_to_script_is_implicit()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo Extends Parent\nFunction Bar()\n  Parent x = None\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var parent = compilation.GetScript(QualifiedName.Parse("Parent"));
        var binder = new ExpressionTypeBinder(compilation, compilation.GetScript(QualifiedName.Parse("Foo")));

        var result = binder.Bind(Initializer(compilation, "Foo"), parent);
        Assert.Equal(ConversionKind.Implicit, result.Kind);
        Assert.Same(parent, result.ResultType);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_parenthesized_none_literal_to_script_is_implicit()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo Extends Parent\nFunction Bar()\n  Parent x = (None)\nEndFunction\n",
            ("Parent", "ScriptName Parent\n"));
        var parent = compilation.GetScript(QualifiedName.Parse("Parent"));
        var binder = new ExpressionTypeBinder(compilation, compilation.GetScript(QualifiedName.Parse("Foo")));

        var result = binder.Bind(Initializer(compilation, "Foo"), parent);
        Assert.Equal(ConversionKind.Implicit, result.Kind);
        Assert.Same(parent, result.ResultType);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_int_literal_to_var_is_implicit()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Var boxed = 1\nEndFunction\n");
        var varType = IntrinsicTypeSymbol.Create(compilation.Names, "Var");
        var binder = new ExpressionTypeBinder(compilation);

        var result = binder.Bind(Initializer(compilation, "Foo"), varType);
        Assert.Equal(ConversionKind.Implicit, result.Kind);
        Assert.Equal(TypeKind.Var, result.ResultType.TypeKind);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void Bind_rejects_int_array_assigned_to_var_array()
    {
        var compilation = Compilation(
            PapyrusDialect.Fallout4,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Var[] items = new Int[1]\nEndFunction\n");
        var varArray = TypeConversions.CreateArrayType(
            IntrinsicTypeSymbol.Create(compilation.Names, "Var"),
            compilation.Names,
            compilation.Dialect);
        var binder = new ExpressionTypeBinder(compilation);

        var result = binder.Bind(Initializer(compilation, "Foo"), varArray);
        Assert.Equal(ConversionKind.None, result.Kind);
        Assert.Equal(PapyrusCompilation.CannotConvertDiagnosticId, result.Diagnostic?.Id);
        Assert.Equal(TypeKind.Array, result.ResultType.TypeKind);
    }

    [Fact]
    public void Classify_rejects_null_arguments()
    {
        var intType = IntrinsicTypeSymbol.Create(new NameTable(), "Int");
        var names = new NameTable();
        Assert.Throws<ArgumentNullException>(() => TypeConversions.ClassifyImplicit(null!, intType));
        Assert.Throws<ArgumentNullException>(() => TypeConversions.ClassifyImplicit(intType, null!));
        Assert.Throws<ArgumentNullException>(() => TypeConversions.ClassifyAs(null!, intType, names));
        Assert.Throws<ArgumentNullException>(() => TypeConversions.ClassifyAs(intType, intType, null!));
        Assert.Throws<ArgumentNullException>(
            () => TypeConversions.ClassifyIs(intType, intType, null!, names));
    }

    private static ExpressionSyntax Initializer(
        PapyrusCompilation compilation,
        string scriptName,
        int statementIndex = 0)
    {
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse(scriptName));
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(statementIndex));
        Assert.NotNull(declare.EqualsValue);
        return declare.EqualsValue.Value;
    }

    private static PapyrusCompilation Compilation(
        PapyrusDialect dialect,
        string name,
        string source,
        params (string Name, string Source)[] extras)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse(name)] = SourceText.From(source),
        };
        foreach (var extra in extras)
        {
            sources[QualifiedName.Parse(extra.Name)] = SourceText.From(extra.Source);
        }

        return PapyrusCompilation.Create(dialect, new TestScriptResolver(sources));
    }

    private static TypeMap CreateTypes()
    {
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.Fallout4,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [QualifiedName.Parse("Object")] = SourceText.From("ScriptName Object\n"),
                [QualifiedName.Parse("Parent")] = SourceText.From(
                    "ScriptName Parent Extends Object\n" +
                    "Struct Point\nFloat X\nEndStruct\n"),
                [QualifiedName.Parse("Child")] = SourceText.From("ScriptName Child Extends Parent\n"),
                [QualifiedName.Parse("Other")] = SourceText.From("ScriptName Other\n"),
            }));

        var names = compilation.Names;
        compilation.GetScript(QualifiedName.Parse("Object"));
        var parent = compilation.GetScript(QualifiedName.Parse("Parent"));
        var child = compilation.GetScript(QualifiedName.Parse("Child"));
        var other = compilation.GetScript(QualifiedName.Parse("Other"));
        var point = Assert.IsAssignableFrom<IStructSymbol>(
            Assert.Single(parent.GetMembers(names.Get("Point"))));
        var integer = IntrinsicTypeSymbol.Create(names, "Int");
        var floating = IntrinsicTypeSymbol.Create(names, "Float");
        var boolean = IntrinsicTypeSymbol.Create(names, "Bool");
        var text = IntrinsicTypeSymbol.Create(names, "String");
        var none = IntrinsicTypeSymbol.Create(names, "None");
        var varType = IntrinsicTypeSymbol.Create(names, "Var");

        return new TypeMap(compilation)
        {
            ["Bool"] = boolean,
            ["Int"] = integer,
            ["Float"] = floating,
            ["String"] = text,
            ["None"] = none,
            ["Var"] = varType,
            ["Parent"] = parent,
            ["Child"] = child,
            ["Other"] = other,
            ["Point"] = point,
            ["Int[]"] = new ArrayTypeSymbol(integer, names),
            ["Var[]"] = new ArrayTypeSymbol(varType, names),
            ["Parent[]"] = new ArrayTypeSymbol(parent, names),
            ["Child[]"] = new ArrayTypeSymbol(child, names),
        };
    }

    private sealed class TypeMap : Dictionary<string, ITypeSymbol>
    {
        public TypeMap(PapyrusCompilation compilation)
            : base(StringComparer.Ordinal)
        {
            Compilation = compilation;
        }

        public PapyrusCompilation Compilation { get; }

        public NameTable Names => Compilation.Names;
    }
}
