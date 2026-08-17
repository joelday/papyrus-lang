using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class LoweringTests
{
    [Fact]
    public void Lower_compound_assign_is_assign_of_binary()
    {
        var lowered = LowerFunction(
            "ScriptName Foo\nFunction Bar()\n  Int x = 1\n  x += 1\nEndFunction\n");

        Assert.Equal(2, lowered.Body.Statements.Length);
        var assign = Assert.IsType<BoundAssignment>(lowered.Body.Statements[1]);
        Assert.Equal(SyntaxKind.EqualsToken, assign.OperatorKind);
        Assert.IsType<BoundLocal>(assign.Left);

        var binary = Assert.IsType<BoundBinary>(assign.Right);
        Assert.Equal(SyntaxKind.PlusToken, binary.OperatorKind);
        Assert.Equal("Int", binary.Type.Name.Text);
        Assert.IsType<BoundLocal>(binary.Left);
        var literal = Assert.IsType<BoundLiteral>(binary.Right);
        Assert.Equal(1, literal.Value);
    }

    [Fact]
    public void Lower_int_plus_equals_inserts_conversion_when_needed()
    {
        var lowered = LowerFunction(
            "ScriptName Foo\nFunction Bar()\n  Float x = 1.0\n  x += 1\nEndFunction\n");

        var assign = Assert.IsType<BoundAssignment>(lowered.Body.Statements[1]);
        Assert.Equal(SyntaxKind.EqualsToken, assign.OperatorKind);
        var binary = Assert.IsType<BoundBinary>(assign.Right);
        Assert.Equal(SyntaxKind.PlusToken, binary.OperatorKind);
        Assert.Equal("Float", binary.Type.Name.Text);
        Assert.IsType<BoundLocal>(binary.Left);

        var conversion = Assert.IsType<BoundConversion>(binary.Right);
        Assert.Equal(ConversionKind.Implicit, conversion.ConversionKind);
        Assert.Equal("Float", conversion.Type.Name.Text);
        var literal = Assert.IsType<BoundLiteral>(conversion.Operand);
        Assert.Equal(1, literal.Value);
        Assert.Equal("Int", literal.Type.Name.Text);
    }

    [Fact]
    public void Lower_auto_property_getter_is_field_load()
    {
        var lowered = LowerFunction(
            "ScriptName Foo\nInt Property Health Auto\nFunction Bar()\n  Int x = Health\nEndFunction\n");

        var declare = Assert.IsType<BoundLocalDeclaration>(Assert.Single(lowered.Body.Statements));
        var field = Assert.IsType<BoundField>(declare.Initializer);
        Assert.Equal("::Health_var", field.Field.Name.Text);
        Assert.Equal("Int", field.Type.Name.Text);
        Assert.Null(field.Receiver);
    }

    [Fact]
    public void Lower_keeps_implicit_conversion_and_string_concat_binary()
    {
        var lowered = LowerFunction(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  Float y = x\n" +
            "  String s = \"a\" + \"b\"\n" +
            "EndFunction\n");

        var conversionDeclare = Assert.IsType<BoundLocalDeclaration>(lowered.Body.Statements[1]);
        var conversion = Assert.IsType<BoundConversion>(conversionDeclare.Initializer);
        Assert.Equal(ConversionKind.Implicit, conversion.ConversionKind);
        Assert.Equal("Float", conversion.Type.Name.Text);

        var concatDeclare = Assert.IsType<BoundLocalDeclaration>(lowered.Body.Statements[2]);
        var concat = Assert.IsType<BoundBinary>(concatDeclare.Initializer);
        Assert.Equal(SyntaxKind.PlusToken, concat.OperatorKind);
        Assert.Equal("String", concat.Type.Name.Text);
        Assert.IsType<BoundLiteral>(concat.Left);
        Assert.IsType<BoundLiteral>(concat.Right);
    }

    private static BoundFunction LowerFunction(
        string source,
        string scriptName = "Foo",
        string functionName = "Bar",
        PapyrusDialect? dialect = null)
    {
        var compilation = Compilation(dialect ?? PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        var function = Assert.IsAssignableFrom<IFunctionSymbol>(
            Assert.Single(script.GetMembers(compilation.Names.Get(functionName))));
        return Lowering.LowerFunction(Binder.BindFunction(function));
    }

    private static PapyrusCompilation Compilation(
        PapyrusDialect dialect,
        string name,
        string source)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse(name)] = SourceText.From(source),
        };
        return PapyrusCompilation.Create(dialect, new TestScriptResolver(sources));
    }
}
