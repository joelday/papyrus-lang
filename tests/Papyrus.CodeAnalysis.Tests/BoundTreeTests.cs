using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class BoundTreeTests
{
    [Fact]
    public void Bind_addition_of_int_literals_is_bound_binary()
    {
        var bound = BindFunction(
            "ScriptName Foo\nFunction Bar()\n  Int x = 1 + 2\nEndFunction\n");

        var declare = Assert.IsType<BoundLocalDeclaration>(Assert.Single(bound.Body.Statements));
        var binary = Assert.IsType<BoundBinary>(declare.Initializer);
        Assert.Equal(SyntaxKind.PlusToken, binary.OperatorKind);
        Assert.Equal("Int", binary.Type.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, binary.Type.TypeKind);

        var left = Assert.IsType<BoundLiteral>(binary.Left);
        var right = Assert.IsType<BoundLiteral>(binary.Right);
        Assert.Equal(1, left.Value);
        Assert.Equal(2, right.Value);
        Assert.Equal("Int", left.Type.Name.Text);
        Assert.Equal("Int", right.Type.Name.Text);
    }

    [Fact]
    public void Bind_int_assigned_to_float_inserts_implicit_conversion()
    {
        var bound = BindFunction(
            "ScriptName Foo\nFunction Bar()\n  Int x = 1\n  Float y = x\nEndFunction\n");

        Assert.Equal(2, bound.Body.Statements.Length);
        var declare = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[1]);
        var conversion = Assert.IsType<BoundConversion>(declare.Initializer);
        Assert.Equal(ConversionKind.Implicit, conversion.ConversionKind);
        Assert.Equal("Float", conversion.Type.Name.Text);
        var local = Assert.IsType<BoundLocal>(conversion.Operand);
        Assert.Equal("x", local.Local.Name.Text);
        Assert.Equal("Int", local.Type.Name.Text);
    }

    [Fact]
    public void Bind_as_float_is_explicit_conversion()
    {
        var bound = BindFunction(
            "ScriptName Foo\nFunction Bar()\n  Int x = 1\n  Float y = x As Float\nEndFunction\n");

        var declare = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[1]);
        var conversion = Assert.IsType<BoundConversion>(declare.Initializer);
        Assert.Equal(ConversionKind.Explicit, conversion.ConversionKind);
        Assert.Equal("Float", conversion.Type.Name.Text);
        var local = Assert.IsType<BoundLocal>(conversion.Operand);
        Assert.Equal("x", local.Local.Name.Text);
    }

    [Fact]
    public void Bind_return_int_is_identity_in_int_function()
    {
        var bound = BindFunction(
            "ScriptName Foo\nInt Function Bar()\n  Return 1\nEndFunction\n");

        var ret = Assert.IsType<BoundReturn>(Assert.Single(bound.Body.Statements));
        var literal = Assert.IsType<BoundLiteral>(ret.Expression);
        Assert.Equal(1, literal.Value);
        Assert.Equal("Int", literal.Type.Name.Text);
        Assert.Equal("Int", bound.Function.ReturnType.Name.Text);
    }

    [Fact]
    public void Bind_return_int_wraps_conversion_in_float_function()
    {
        var bound = BindFunction(
            "ScriptName Foo\nFloat Function Bar()\n  Return 1\nEndFunction\n");

        var ret = Assert.IsType<BoundReturn>(Assert.Single(bound.Body.Statements));
        var conversion = Assert.IsType<BoundConversion>(ret.Expression);
        Assert.Equal(ConversionKind.Implicit, conversion.ConversionKind);
        Assert.Equal("Float", conversion.Type.Name.Text);
        var literal = Assert.IsType<BoundLiteral>(conversion.Operand);
        Assert.Equal(1, literal.Value);
        Assert.Equal("Int", literal.Type.Name.Text);
    }

    [Fact]
    public void Bind_if_while_and_locals()
    {
        var bound = BindFunction(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  EndIf\n" +
            "  While x\n" +
            "    Return\n" +
            "  EndWhile\n" +
            "EndFunction\n");

        Assert.Equal(3, bound.Body.Statements.Length);

        var declare = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[0]);
        Assert.Equal("x", declare.Local.Name.Text);
        Assert.Equal("Int", declare.Local.Type.Name.Text);
        var initializer = Assert.IsType<BoundLiteral>(declare.Initializer);
        Assert.Equal(1, initializer.Value);

        var @if = Assert.IsType<BoundIf>(bound.Body.Statements[1]);
        var ifCondition = Assert.IsType<BoundConversion>(@if.Condition);
        Assert.Equal(ConversionKind.Implicit, ifCondition.ConversionKind);
        Assert.Equal("Bool", ifCondition.Type.Name.Text);
        Assert.IsType<BoundLocal>(ifCondition.Operand);
        var assign = Assert.IsType<BoundAssignment>(Assert.Single(@if.Then.Statements));
        Assert.Equal(SyntaxKind.EqualsToken, assign.OperatorKind);
        Assert.IsType<BoundLocal>(assign.Left);
        Assert.IsType<BoundLiteral>(assign.Right);

        var @while = Assert.IsType<BoundWhile>(bound.Body.Statements[2]);
        var whileCondition = Assert.IsType<BoundConversion>(@while.Condition);
        Assert.Equal("Bool", whileCondition.Type.Name.Text);
        Assert.IsType<BoundReturn>(Assert.Single(@while.Body.Statements));
    }

    [Fact]
    public void Bind_unrelated_script_as_is_none_typed_conversion()
    {
        var bound = BindFunction(
            "ScriptName Child Extends Parent\nFunction Bar()\n  Other o = None\n  Parent p = o As Parent\nEndFunction\n",
            scriptName: "Child",
            dialect: PapyrusDialect.Fallout4,
            extras:
            [
                ("Parent", "ScriptName Parent\n"),
                ("Other", "ScriptName Other\n"),
            ]);

        var declare = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[1]);
        var conversion = Assert.IsType<BoundConversion>(declare.Initializer);
        Assert.Equal(ConversionKind.None, conversion.ConversionKind);
        Assert.Equal(TypeKind.Intrinsic, conversion.Type.TypeKind);
        Assert.Equal("None", conversion.Type.Name.Text);
        Assert.IsType<BoundLocal>(conversion.Operand);
    }

    [Fact]
    public void Bind_self_parent_call_and_array_length()
    {
        var bound = BindFunction(
            "ScriptName Child Extends Parent\nFunction Bar()\n" +
            "  Self.Go()\n" +
            "  Parent.Go()\n" +
            "  Int[] items = new Int[2]\n" +
            "  Int n = items.Length\n" +
            "EndFunction\n",
            scriptName: "Child",
            extras: [("Parent", "ScriptName Parent\nFunction Go()\nEndFunction\n")]);

        Assert.Equal(4, bound.Body.Statements.Length);

        var selfCall = Assert.IsType<BoundExpressionStatement>(bound.Body.Statements[0]);
        var call = Assert.IsType<BoundCall>(selfCall.Expression);
        Assert.Equal("Go", call.Method?.Name.Text);
        Assert.IsType<BoundSelf>(call.Receiver);

        var parentCall = Assert.IsType<BoundExpressionStatement>(bound.Body.Statements[1]);
        var parent = Assert.IsType<BoundCall>(parentCall.Expression);
        Assert.IsType<BoundParent>(parent.Receiver);

        var created = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[2]);
        var array = Assert.IsType<BoundNewArray>(created.Initializer);
        Assert.Equal(TypeKind.Array, array.Type.TypeKind);
        Assert.Equal("Int", array.ElementType.Name.Text);

        var length = Assert.IsType<BoundLocalDeclaration>(bound.Body.Statements[3]);
        var boundLength = Assert.IsType<BoundArrayLength>(length.Initializer);
        Assert.Equal("Int", boundLength.Type.Name.Text);
        Assert.IsType<BoundLocal>(boundLength.Array);
    }

    [Fact]
    public void Bind_script_includes_function_bodies()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Return\nEndFunction\n");
        var script = compilation.GetScript(QualifiedName.Parse("Foo"));

        var bound = Binder.BindScript(script);

        Assert.Same(script, bound.Script);
        var function = Assert.Single(bound.Functions);
        Assert.Equal("Bar", function.Function.Name.Text);
        Assert.IsType<BoundReturn>(Assert.Single(function.Body.Statements));
    }

    private static BoundFunction BindFunction(
        string source,
        string scriptName = "Foo",
        string functionName = "Bar",
        PapyrusDialect? dialect = null,
        params (string Name, string Source)[] extras)
    {
        var compilation = Compilation(dialect ?? PapyrusDialect.SkyrimSe, scriptName, source, extras);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        var function = Assert.IsAssignableFrom<IFunctionSymbol>(
            Assert.Single(script.GetMembers(compilation.Names.Get(functionName))));
        return Binder.BindFunction(function);
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
}
