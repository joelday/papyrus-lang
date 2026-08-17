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
        Assert.Equal("Foo", root.Name.Identifier.Text);
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
        Assert.Equal("Foo", root.Name.Identifier.Text);
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
        Assert.Equal("Foo", root.Name.Identifier.Text);
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
        var ret = Assert.IsType<ReturnStatementSyntax>(function.Statements.Get(0));
        var returned = Assert.IsType<NameExpressionSyntax>(ret.Expression);
        Assert.Equal("x", ((IdentifierNameSyntax)returned.Name).Identifier.Text);
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
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.Equal("Var", ((IdentifierNameSyntax)declare.TypeName.Name).Identifier.Text);
        Assert.Equal("v", declare.Name.Identifier.Text);
        Assert.False(function.EndFunctionKeyword.IsMissing);
    }

    [Fact]
    public void Parse_if_elseif_else_inside_function()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function Bar()\n" +
            "  If x\n" +
            "    Return 1\n" +
            "  ElseIf y\n" +
            "    Return 2\n" +
            "  Else\n" +
            "    Return\n" +
            "  EndIf\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var ifStatement = Assert.IsType<IfStatementSyntax>(function.Statements.Get(0));
        Assert.False(ifStatement.IfKeyword.IsMissing);
        Assert.Equal("x", ((IdentifierNameSyntax)((NameExpressionSyntax)ifStatement.Condition).Name).Identifier.Text);
        Assert.Equal(1, ifStatement.Statements.Count);
        Assert.IsType<ReturnStatementSyntax>(ifStatement.Statements.Get(0));
        Assert.Equal(1, ifStatement.ElseIfClauses.Count);
        Assert.Equal("y", ((IdentifierNameSyntax)((NameExpressionSyntax)ifStatement.ElseIfClauses.Get(0).Condition).Name).Identifier.Text);
        Assert.NotNull(ifStatement.ElseClause);
        Assert.Equal(1, ifStatement.ElseClause.Statements.Count);
        var bareReturn = Assert.IsType<ReturnStatementSyntax>(ifStatement.ElseClause.Statements.Get(0));
        Assert.Null(bareReturn.Expression);
        Assert.False(ifStatement.EndIfKeyword.IsMissing);
    }

    [Fact]
    public void Parse_nested_if_inside_function()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function Bar()\n" +
            "  If a\n" +
            "    If b\n" +
            "      Return\n" +
            "    EndIf\n" +
            "  EndIf\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var outer = Assert.IsType<IfStatementSyntax>(function.Statements.Get(0));
        var inner = Assert.IsType<IfStatementSyntax>(outer.Statements.Get(0));
        Assert.Equal("b", ((IdentifierNameSyntax)((NameExpressionSyntax)inner.Condition).Name).Identifier.Text);
        Assert.False(inner.EndIfKeyword.IsMissing);
        Assert.False(outer.EndIfKeyword.IsMissing);
    }

    [Fact]
    public void Missing_endif_produces_missing_token_and_does_not_swallow_endfunction()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  If x\n    Return\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var ifStatement = Assert.IsType<IfStatementSyntax>(function.Statements.Get(0));
        Assert.True(ifStatement.EndIfKeyword.IsMissing);
        Assert.Equal(0, ifStatement.EndIfKeyword.FullWidth);
        Assert.False(function.EndFunctionKeyword.IsMissing);
    }

    [Fact]
    public void Parse_while_inside_function()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function Bar()\n" +
            "  While x == 1\n" +
            "    Return\n" +
            "  EndWhile\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var whileStatement = Assert.IsType<WhileStatementSyntax>(function.Statements.Get(0));
        Assert.False(whileStatement.WhileKeyword.IsMissing);
        var condition = Assert.IsType<BinaryExpressionSyntax>(whileStatement.Condition);
        Assert.Equal(SyntaxKind.EqualsEqualsToken, condition.OperatorToken.Kind);
        Assert.Equal(1, whileStatement.Statements.Count);
        Assert.IsType<ReturnStatementSyntax>(whileStatement.Statements.Get(0));
        Assert.False(whileStatement.EndWhileKeyword.IsMissing);
    }

    [Fact]
    public void Parse_declare_and_assign_inside_function()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function Bar()\n" +
            "  Int x = 1\n" +
            "  x = 2\n" +
            "  x += 3\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(3, function.Statements.Count);

        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.Equal("Int", ((IdentifierNameSyntax)declare.TypeName.Name).Identifier.Text);
        Assert.Equal("x", declare.Name.Identifier.Text);
        Assert.NotNull(declare.EqualsValue);
        Assert.Equal("1", ((LiteralExpressionSyntax)declare.EqualsValue.Value).Token.Text);

        var assign = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(1));
        Assert.Equal("x", ((IdentifierNameSyntax)((NameExpressionSyntax)assign.Left).Name).Identifier.Text);
        Assert.Equal(SyntaxKind.EqualsToken, assign.OperatorToken.Kind);
        Assert.Equal("2", ((LiteralExpressionSyntax)assign.Right).Token.Text);

        var compound = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(2));
        Assert.Equal(SyntaxKind.PlusEqualsToken, compound.OperatorToken.Kind);
        Assert.Equal("3", ((LiteralExpressionSyntax)compound.Right).Token.Text);
    }

    [Fact]
    public void Parse_expression_statement_call_inside_function()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Foo()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal(1, function.Statements.Count);
        var exprStmt = Assert.IsType<ExpressionStatementSyntax>(function.Statements.Get(0));
        var name = Assert.IsType<NameExpressionSyntax>(exprStmt.Expression);
        Assert.Equal("Foo", ((IdentifierNameSyntax)name.Name).Identifier.Text);
    }

    [Theory]
    [InlineData("ScriptName Foo\nFunction F()\nInt x = 1\nEndFunction\n")]
    [InlineData("ScriptName Foo\nFunction F()\n  Int x = 1\nEndFunction\n")]
    public void Local_int_declare_parses_as_declare_statement(string source)
    {
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.Equal("x", declare.Name.Identifier.Text);
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
}
