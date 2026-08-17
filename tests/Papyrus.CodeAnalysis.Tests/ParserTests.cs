using System;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Parse_script_name_round_trips()
    {
        const string source = "ScriptName Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.ScriptDeclaration, root.Kind);
        Assert.False(root.ScriptNameKeyword.IsMissing);
        Assert.Equal("Foo", ((IdentifierNameSyntax)root.Name).Identifier.Text);
        Assert.Null(root.ExtendsClause);
        Assert.Empty(root.Imports);
    }

    [Fact]
    public void Parse_script_name_extends_round_trips()
    {
        const string source = "ScriptName Foo Extends Bar\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal("Foo", ((IdentifierNameSyntax)root.Name).Identifier.Text);
        Assert.NotNull(root.ExtendsClause);
        Assert.Equal("Bar", ((IdentifierNameSyntax)root.ExtendsClause.TypeName.Name).Identifier.Text);
    }

    [Fact]
    public void Missing_script_name_produces_missing_token_and_does_not_throw()
    {
        var tree = SyntaxTree.Parse("Foo\n", PapyrusDialect.SkyrimSe);

        Assert.Equal("Foo\n", tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
        Assert.Equal(0, root.ScriptNameKeyword.FullWidth);
        Assert.Equal("Foo", ((IdentifierNameSyntax)root.Name).Identifier.Text);
        Assert.False(root.Name.IsMissing);
    }

    [Fact]
    public void Empty_input_is_all_missing_and_does_not_throw()
    {
        var tree = SyntaxTree.Parse(string.Empty, PapyrusDialect.SkyrimSe);

        Assert.Equal(string.Empty, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
        Assert.True(root.Name.IsMissing);
        Assert.True(root.EndOfLineToken.IsMissing);
    }

    [Fact]
    public void Parse_imports_and_flags()
    {
        const string source = "ScriptName Foo Hidden Conditional\nImport Utility\nImport Game\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.HiddenKeyword, root.HiddenKeyword.Kind);
        Assert.Equal(SyntaxKind.ConditionalKeyword, root.ConditionalKeyword.Kind);
        Assert.Equal(2, root.Imports.Count);
        Assert.Equal("Utility", ((IdentifierNameSyntax)root.Imports.Get(0).Name.Name).Identifier.Text);
        Assert.Equal("Game", ((IdentifierNameSyntax)root.Imports.Get(1).Name.Name).Identifier.Text);
    }

    [Fact]
    public void Doc_comment_attaches_to_script_declaration()
    {
        const string source = "{A script}\nScriptName Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.DocCommentToken, root.DocComment.Kind);
        Assert.Equal("{A script}", root.DocComment.Text);
    }

    [Fact]
    public void Garbage_input_does_not_throw_and_round_trips()
    {
        const string source = "@#$\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.True(root.ScriptNameKeyword.IsMissing);
    }

    [Fact]
    public void Parse_source_text_with_options()
    {
        const string source = "ScriptName Foo\n";
        var tree = SyntaxTree.Parse(SourceText.From(source), PapyrusParseOptions.Default);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.Same(PapyrusDialect.SkyrimSe, tree.Dialect);
    }

    [Theory]
    [InlineData("ScriptName Foo Hidden Native\n")]
    [InlineData("ScriptName Foo Conditional Hidden\n")]
    [InlineData("ScriptName Foo Conditional Native Hidden\n")]
    public void Out_of_order_flags_are_named_and_round_trip(string source)
    {
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        if (source.Contains("Native", StringComparison.Ordinal))
        {
            Assert.Equal(SyntaxKind.NativeKeyword, root.NativeKeyword.Kind);
            Assert.Equal("Native", root.NativeKeyword.Text);
        }

        if (source.Contains("Hidden", StringComparison.Ordinal))
        {
            Assert.Equal(SyntaxKind.HiddenKeyword, root.HiddenKeyword.Kind);
            Assert.Equal("Hidden", root.HiddenKeyword.Text);
        }

        if (source.Contains("Conditional", StringComparison.Ordinal))
        {
            Assert.Equal(SyntaxKind.ConditionalKeyword, root.ConditionalKeyword.Kind);
            Assert.Equal("Conditional", root.ConditionalKeyword.Text);
        }
    }

    [Fact]
    public void Cancelled_parse_keeps_remaining_tokens_on_eof()
    {
        const string source = "ScriptName Foo\nImport Utility\nFunction Bar()\n";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var tree = SyntaxTree.Parse(SourceText.From(source), PapyrusDialect.SkyrimSe, options: null, cts.Token);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(SyntaxKind.EndOfFileToken, root.EndOfFileToken.Kind);
        Assert.Empty(root.Imports);
        Assert.Contains("Import", root.EndOfFileToken.ToFullString(), StringComparison.Ordinal);
        Assert.Contains("Function", root.EndOfFileToken.ToFullString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_empty_function_round_trips()
    {
        const string source = "ScriptName Foo\nFunction Bar()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(1, root.Members.Count);
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Bar", function.Name.Identifier.Text);
        Assert.Null(function.ReturnType);
        Assert.False(function.FunctionKeyword.IsMissing);
        Assert.False(function.ParameterList.OpenParenToken.IsMissing);
        Assert.False(function.ParameterList.CloseParenToken.IsMissing);
        Assert.Empty(function.ParameterList.Parameters);
        Assert.Empty(function.Statements);
        Assert.False(function.EndFunctionKeyword.IsMissing);
        Assert.Equal(SyntaxKind.EndFunctionKeyword, function.EndFunctionKeyword.Kind);
    }

    [Fact]
    public void Parse_typed_function_with_parameters_and_body_round_trips()
    {
        const string source = "ScriptName Foo\nInt Function Bar(Int x, String y = \"z\")\n  Return x\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.NotNull(function.ReturnType);
        Assert.Equal("Int", ((IdentifierNameSyntax)function.ReturnType.Name).Identifier.Text);
        Assert.Equal(2, function.ParameterList.Parameters.Count);
        Assert.Equal("x", function.ParameterList.Parameters.Get(0).Name.Identifier.Text);
        Assert.Equal("y", function.ParameterList.Parameters.Get(1).Name.Identifier.Text);
        Assert.Equal(SyntaxKind.CommaToken, function.ParameterList.Parameters.Get(1).CommaToken.Kind);
        var defaultValue = function.ParameterList.Parameters.Get(1).EqualsValue;
        Assert.NotNull(defaultValue);
        Assert.Equal("\"z\"", ((LiteralExpressionSyntax)defaultValue.Value).Token.Text);
        Assert.Equal(1, function.Statements.Count);
        Assert.IsType<SkippedStatementSyntax>(function.Statements.Get(0));
    }

    [Fact]
    public void Parse_native_function_has_no_end_function()
    {
        const string source = "ScriptName Foo\nInt Function NativeFunction() Native\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(SyntaxKind.NativeKeyword, function.NativeKeyword.Kind);
        Assert.Equal(SyntaxKind.None, function.EndFunctionKeyword.Kind);
        Assert.Empty(function.Statements);
    }

    [Fact]
    public void Missing_end_function_produces_missing_token_and_does_not_throw()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.True(function.EndFunctionKeyword.IsMissing);
        Assert.Equal(0, function.EndFunctionKeyword.FullWidth);
    }

    [Fact]
    public void Missing_end_function_does_not_swallow_the_next_function()
    {
        const string source = "ScriptName Foo\nFunction Bar()\nInt Function Baz()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        var bar = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var baz = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        Assert.Equal("Bar", bar.Name.Identifier.Text);
        Assert.True(bar.EndFunctionKeyword.IsMissing);
        Assert.Equal("Baz", baz.Name.Identifier.Text);
        Assert.False(baz.EndFunctionKeyword.IsMissing);
    }

    [Theory]
    [InlineData("ScriptName Foo\nFunction F()\nInt x = 1\nEndFunction\n")]
    [InlineData("ScriptName Foo\nFunction F()\n  Int x = 1\nEndFunction\n")]
    [InlineData("ScriptName Foo\nFunction F()\n  x = y as Int\nEndFunction\n")]
    [InlineData("ScriptName Foo\nFunction F()\n  x = new Int[5]\nEndFunction\n")]
    [InlineData("ScriptName Foo\nFunction F()\n  String s = \"a\"\n  Float f = 1.0\n  Bool b = True\nEndFunction\n")]
    [InlineData("ScriptName Foo\nEvent OnInit()\n  Int x = 1\nEndEvent\n")]
    [InlineData("ScriptName Foo\nFunction Bar()\nInt Count = 2\n")]
    public void Function_body_with_type_keywords_does_not_hang_and_round_trips(string source)
    {
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(1, root.Members.Count);
        Assert.False(root.Members.Get(0).IsMissing);
    }

    [Fact]
    public void Fo4_var_local_in_function_body_does_not_hang()
    {
        const string source = "ScriptName Foo\nFunction F()\n  Var v = 1\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(1, function.Statements.Count);
        Assert.False(function.EndFunctionKeyword.IsMissing);
    }

    [Fact]
    public void Parse_event_round_trips()
    {
        const string source = "ScriptName Foo\nEvent OnInit()\nEndEvent\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var ev = Assert.IsType<EventDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("OnInit", ev.Name.Identifier.Text);
        Assert.False(ev.EndEventKeyword.IsMissing);
    }

    [Fact]
    public void Parse_auto_property_round_trips()
    {
        const string source = "ScriptName Foo\nInt Property Value Auto\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Int", ((IdentifierNameSyntax)property.TypeName.Name).Identifier.Text);
        Assert.Equal("Value", property.Name.Identifier.Text);
        Assert.Equal(SyntaxKind.AutoKeyword, property.AutoKeyword.Kind);
        Assert.Equal(SyntaxKind.None, property.EndPropertyKeyword.Kind);
        Assert.Empty(property.Accessors);
    }

    [Fact]
    public void Parse_auto_property_with_initializer_round_trips()
    {
        const string source = "ScriptName Foo\nInt Property Value = 5 Auto\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        Assert.NotNull(property.EqualsValue);
        Assert.Equal("5", ((LiteralExpressionSyntax)property.EqualsValue.Value).Token.Text);
        Assert.Equal(SyntaxKind.AutoKeyword, property.AutoKeyword.Kind);
    }

    [Fact]
    public void Parse_full_property_round_trips()
    {
        const string source = "ScriptName Foo\nInt Property Value\nInt Function Get()\nEndFunction\nEndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(SyntaxKind.None, property.AutoKeyword.Kind);
        Assert.Equal(1, property.Accessors.Count);
        Assert.Equal("Get", property.Accessors.Get(0).Name.Identifier.Text);
        Assert.False(property.EndPropertyKeyword.IsMissing);
    }

    [Fact]
    public void Parse_full_property_doc_comment_stays_on_typed_getter()
    {
        const string source = "ScriptName Foo\nInt Property Value\n{getter}\nInt Function Get()\nEndFunction\nEndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(1, root.Members.Count);
        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(1, property.Accessors.Count);
        Assert.Equal("Get", property.Accessors.Get(0).Name.Identifier.Text);
        Assert.Equal("{getter}", property.Accessors.Get(0).DocComment.Text);
        Assert.False(property.EndPropertyKeyword.IsMissing);
    }

    [Fact]
    public void Parse_full_property_doc_comment_stays_on_untyped_getter()
    {
        const string source = "ScriptName Foo\nInt Property Value\n{getter}\nFunction Get()\nEndFunction\nEndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(1, root.Members.Count);
        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(1, property.Accessors.Count);
        Assert.Equal("Get", property.Accessors.Get(0).Name.Identifier.Text);
        Assert.Equal("{getter}", property.Accessors.Get(0).DocComment.Text);
        Assert.False(property.EndPropertyKeyword.IsMissing);
    }

    [Fact]
    public void Parse_variable_round_trips()
    {
        const string source = "ScriptName Foo\nInt Count = -1 Conditional\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Count", variable.Name.Identifier.Text);
        Assert.NotNull(variable.EqualsValue);
        var unary = Assert.IsType<PrefixUnaryExpressionSyntax>(variable.EqualsValue.Value);
        Assert.Equal(SyntaxKind.MinusToken, unary.OperatorToken.Kind);
        Assert.Equal("-1", unary.ToFullString().Trim());
        Assert.Equal(SyntaxKind.ConditionalKeyword, variable.ConditionalKeyword.Kind);
    }

    [Fact]
    public void Parse_state_round_trips()
    {
        const string source = "ScriptName Foo\nState Combat\nFunction OnUpdate()\nEndFunction\nEndState\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var state = Assert.IsType<StateDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Combat", state.Name.Identifier.Text);
        Assert.Equal(SyntaxKind.None, state.AutoKeyword.Kind);
        Assert.Equal(1, state.Members.Count);
        var function = Assert.IsType<FunctionDeclarationSyntax>(state.Members.Get(0));
        Assert.Equal("OnUpdate", function.Name.Identifier.Text);
        Assert.False(state.EndStateKeyword.IsMissing);
    }

    [Fact]
    public void Parse_auto_state_round_trips()
    {
        const string source = "ScriptName Foo\nAuto State Default\nEvent OnInit()\nEndEvent\nEndState\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var state = Assert.IsType<StateDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(SyntaxKind.AutoKeyword, state.AutoKeyword.Kind);
        Assert.Equal("Default", state.Name.Identifier.Text);
        Assert.Equal(1, state.Members.Count);
        Assert.IsType<EventDeclarationSyntax>(state.Members.Get(0));
    }

    [Fact]
    public void Parse_doc_comment_attaches_to_function()
    {
        const string source = "ScriptName Foo\n{A function}\nFunction Bar()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("{A function}", function.DocComment.Text);
    }

    [Fact]
    public void Parse_mixed_members_round_trips()
    {
        const string source =
            "ScriptName Foo Hidden\n" +
            "Import Utility\n" +
            "Int Count = 0\n" +
            "Int Property Value Auto\n" +
            "Function Bar(Int x = 1) Global\n" +
            "  Count = x\n" +
            "EndFunction\n" +
            "Auto State Default\n" +
            "Event OnInit()\n" +
            "EndEvent\n" +
            "EndState\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(1, root.Imports.Count);
        Assert.Equal(4, root.Members.Count);
        Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(1));
        Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(2));
        Assert.IsType<StateDeclarationSyntax>(root.Members.Get(3));
    }

    [Fact]
    public void Parse_namespaced_script_name_round_trips()
    {
        const string source = "ScriptName A:B:Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var qualified = Assert.IsType<QualifiedNameSyntax>(root.Name);
        var left = Assert.IsType<QualifiedNameSyntax>(qualified.Left);
        Assert.Equal("A", ((IdentifierNameSyntax)left.Left).Identifier.Text);
        Assert.Equal("B", left.Right.Identifier.Text);
        Assert.Equal("Foo", qualified.Right.Identifier.Text);
    }

    [Fact]
    public void Parse_namespaced_script_name_extends_and_import_round_trips()
    {
        const string source = "ScriptName A:B:Foo Extends A:B:Bar\nImport A:B:Utility\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.IsType<QualifiedNameSyntax>(root.Name);
        Assert.NotNull(root.ExtendsClause);
        Assert.IsType<QualifiedNameSyntax>(root.ExtendsClause.TypeName.Name);
        Assert.Equal(1, root.Imports.Count);
        Assert.IsType<QualifiedNameSyntax>(root.Imports.Get(0).Name.Name);
    }

    [Fact]
    public void Skyrim_does_not_parse_colon_as_script_namespace()
    {
        const string source = "ScriptName A:B:Foo\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var name = Assert.IsType<IdentifierNameSyntax>(root.Name);
        Assert.Equal("A", name.Identifier.Text);
    }

    [Fact]
    public void Parse_struct_round_trips()
    {
        const string source = "ScriptName Foo\nStruct Point\nFloat X\nFloat Y = 0.0\nEndStruct\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var structure = Assert.IsType<StructDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Point", structure.Name.Identifier.Text);
        Assert.Equal(2, structure.Members.Count);
        Assert.Equal("X", structure.Members.Get(0).Name.Identifier.Text);
        Assert.Equal("Y", structure.Members.Get(1).Name.Identifier.Text);
        Assert.NotNull(structure.Members.Get(1).EqualsValue);
        Assert.False(structure.EndStructKeyword.IsMissing);
    }

    [Fact]
    public void Parse_struct_doc_comment_and_member_docs_round_trip()
    {
        const string source = "ScriptName Foo\n{A point}\nStruct Point\n{x}\nFloat X\nEndStruct\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var structure = Assert.IsType<StructDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("{A point}", structure.DocComment.Text);
        Assert.Equal("{x}", structure.Members.Get(0).DocComment.Text);
    }

    [Fact]
    public void Missing_end_struct_does_not_swallow_the_next_function()
    {
        const string source = "ScriptName Foo\nStruct Point\nFloat X\nInt Function Bar()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        var structure = Assert.IsType<StructDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Point", structure.Name.Identifier.Text);
        Assert.Equal(1, structure.Members.Count);
        Assert.True(structure.EndStructKeyword.IsMissing);
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        Assert.Equal("Bar", function.Name.Identifier.Text);
    }

    [Fact]
    public void Skyrim_struct_is_an_identifier_not_a_declaration()
    {
        const string source = "ScriptName Foo\nStruct Name\nInt X\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Struct", ((IdentifierNameSyntax)variable.TypeName.Name).Identifier.Text);
        Assert.Equal("Name", variable.Name.Identifier.Text);
        Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(1));
    }

    [Fact]
    public void Parse_group_round_trips()
    {
        const string source = "ScriptName Foo\nGroup Stats Collapsed\nInt Property Health Auto\nInt Property Magicka Auto\nEndGroup\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var group = Assert.IsType<GroupDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Stats", group.Name.Identifier.Text);
        Assert.Equal(1, group.Flags.Count);
        Assert.Equal("Collapsed", group.Flags.Get(0).FlagToken.Text);
        Assert.Equal(2, group.Members.Count);
        Assert.Equal("Health", group.Members.Get(0).Name.Identifier.Text);
        Assert.Equal("Magicka", group.Members.Get(1).Name.Identifier.Text);
        Assert.False(group.EndGroupKeyword.IsMissing);
    }

    [Fact]
    public void Parse_group_property_doc_comment_round_trips()
    {
        const string source = "ScriptName Foo\nGroup Stats\n{hp}\nInt Property Health Auto\nEndGroup\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var group = Assert.IsType<GroupDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(1, group.Members.Count);
        Assert.Equal("{hp}", group.Members.Get(0).DocComment.Text);
    }

    [Fact]
    public void Missing_end_group_does_not_swallow_the_next_function()
    {
        const string source = "ScriptName Foo\nGroup Stats\nInt Property Health Auto\nFunction Bar()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        var group = Assert.IsType<GroupDeclarationSyntax>(root.Members.Get(0));
        Assert.True(group.EndGroupKeyword.IsMissing);
        Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
    }

    [Fact]
    public void Parse_custom_event_round_trips()
    {
        const string source = "ScriptName Foo\nCustomEvent OnSomething\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var customEvent = Assert.IsType<CustomEventDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("OnSomething", customEvent.Name.Identifier.Text);
        Assert.False(customEvent.CustomEventKeyword.IsMissing);
    }

    [Fact]
    public void Parse_custom_event_doc_comment_round_trips()
    {
        const string source = "ScriptName Foo\n{Fired when X}\nCustomEvent OnSomething\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var customEvent = Assert.IsType<CustomEventDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("{Fired when X}", customEvent.DocComment.Text);
    }

    [Fact]
    public void Skyrim_custom_event_is_an_identifier_not_a_declaration()
    {
        const string source = "ScriptName Foo\nCustomEvent OnSomething\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("CustomEvent", ((IdentifierNameSyntax)variable.TypeName.Name).Identifier.Text);
        Assert.Equal("OnSomething", variable.Name.Identifier.Text);
    }

    [Fact]
    public void Parse_fo4_mixed_members_round_trips()
    {
        const string source =
            "ScriptName A:B:Foo Hidden\n" +
            "Import A:B:Utility\n" +
            "CustomEvent OnSomething\n" +
            "Struct Point\n" +
            "Float X\n" +
            "Float Y\n" +
            "EndStruct\n" +
            "Group Stats\n" +
            "Int Property Health Auto\n" +
            "EndGroup\n" +
            "Function Bar()\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.IsType<QualifiedNameSyntax>(root.Name);
        Assert.Equal(1, root.Imports.Count);
        Assert.Equal(4, root.Members.Count);
        Assert.IsType<CustomEventDeclarationSyntax>(root.Members.Get(0));
        Assert.IsType<StructDeclarationSyntax>(root.Members.Get(1));
        Assert.IsType<GroupDeclarationSyntax>(root.Members.Get(2));
        Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(3));
    }

    [Fact]
    public void Starfield_parses_fo4_constructs_but_not_guard()
    {
        const string source = "ScriptName A:Foo\nStruct Point\nInt X\nEndStruct\nGuard\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.IsType<QualifiedNameSyntax>(root.Name);
        Assert.Equal(1, root.Members.Count);
        Assert.IsType<StructDeclarationSyntax>(root.Members.Get(0));
    }

    [Theory]
    [InlineData("ScriptName A:B:Foo\n")]
    [InlineData("ScriptName Foo\nStruct Point\nFloat X = 1.0\nEndStruct\n")]
    [InlineData("ScriptName Foo\nGroup Stats Collapsed\nInt Property Health Auto\nEndGroup\n")]
    [InlineData("ScriptName Foo\nCustomEvent OnSomething\n")]
    [InlineData("ScriptName BaseScript Extends ScriptObject\nCustomEvent baseScriptCustomEvent\nGroup PropertyGroup\nInt Property GroupProperty Auto\nEndGroup\nStruct StructA\nInt structIntVariable\nString structStringVariable\nScriptObject object\nEndStruct\n")]
    public void Well_formed_fo4_snippets_round_trip(string source)
    {
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.False(root.IsMissing);
    }
}
