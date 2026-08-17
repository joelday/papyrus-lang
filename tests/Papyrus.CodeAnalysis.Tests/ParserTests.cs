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
        var call = Assert.IsType<InvocationExpressionSyntax>(exprStmt.Expression);
        var name = Assert.IsType<NameExpressionSyntax>(call.Expression);
        Assert.Equal("Foo", ((IdentifierNameSyntax)name.Name).Identifier.Text);
        Assert.False(call.ArgumentList.OpenParenToken.IsMissing);
        Assert.False(call.ArgumentList.CloseParenToken.IsMissing);
        Assert.Empty(call.ArgumentList.Arguments);
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
    public void Local_declare_initializer_keeps_additive_binary()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Int x = 1 + 2\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.NotNull(declare.EqualsValue);
        var binary = Assert.IsType<BinaryExpressionSyntax>(declare.EqualsValue.Value);
        Assert.Equal(SyntaxKind.PlusToken, binary.OperatorToken.Kind);
        Assert.Equal("1", ((LiteralExpressionSyntax)binary.Left).Token.Text);
        Assert.Equal("2", ((LiteralExpressionSyntax)binary.Right).Token.Text);
    }

    [Fact]
    public void Local_declare_initializer_keeps_comparison_binary()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Bool b = x == 0\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.NotNull(declare.EqualsValue);
        var binary = Assert.IsType<BinaryExpressionSyntax>(declare.EqualsValue.Value);
        Assert.Equal(SyntaxKind.EqualsEqualsToken, binary.OperatorToken.Kind);
        Assert.Equal("x", ((IdentifierNameSyntax)((NameExpressionSyntax)binary.Left).Name).Identifier.Text);
        Assert.Equal("0", ((LiteralExpressionSyntax)binary.Right).Token.Text);
    }

    [Fact]
    public void Additive_does_not_bind_tighter_than_multiplicative()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Int x = 1+2*3\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));
        Assert.NotNull(declare.EqualsValue);
        var add = Assert.IsType<BinaryExpressionSyntax>(declare.EqualsValue.Value);
        Assert.Equal(SyntaxKind.PlusToken, add.OperatorToken.Kind);
        Assert.Equal("1", ((LiteralExpressionSyntax)add.Left).Token.Text);
        var mul = Assert.IsType<BinaryExpressionSyntax>(add.Right);
        Assert.Equal(SyntaxKind.AsteriskToken, mul.OperatorToken.Kind);
        Assert.Equal("2", ((LiteralExpressionSyntax)mul.Left).Token.Text);
        Assert.Equal("3", ((LiteralExpressionSyntax)mul.Right).Token.Text);
    }

    [Fact]
    public void Parse_as_cast_expression()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  x = y as Int\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var assign = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(0));
        var cast = Assert.IsType<CastExpressionSyntax>(assign.Right);
        Assert.Equal("y", ((IdentifierNameSyntax)((NameExpressionSyntax)cast.Expression).Name).Identifier.Text);
        Assert.Equal(SyntaxKind.AsKeyword, cast.OperatorToken.Kind);
        Assert.Equal("Int", ((IdentifierNameSyntax)cast.Type.Name).Identifier.Text);
    }

    [Fact]
    public void Parse_new_array_expression()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  x = new Int[5]\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var assign = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(0));
        var created = Assert.IsType<NewExpressionSyntax>(assign.Right);
        Assert.False(created.NewKeyword.IsMissing);
        Assert.Equal("Int", ((IdentifierNameSyntax)created.TypeName.Name).Identifier.Text);
        Assert.Equal(SyntaxKind.None, created.TypeName.OpenBracketToken.Kind);
        Assert.False(created.OpenBracketToken.IsMissing);
        Assert.NotNull(created.Size);
        Assert.Equal("5", ((LiteralExpressionSyntax)created.Size).Token.Text);
        Assert.False(created.CloseBracketToken.IsMissing);
    }

    [Fact]
    public void Member_access_with_missing_name_is_well_formed()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Foo.\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var exprStmt = Assert.IsType<ExpressionStatementSyntax>(function.Statements.Get(0));
        var member = Assert.IsType<MemberAccessExpressionSyntax>(exprStmt.Expression);
        Assert.Equal("Foo", ((IdentifierNameSyntax)((NameExpressionSyntax)member.Expression).Name).Identifier.Text);
        Assert.False(member.DotToken.IsMissing);
        Assert.True(member.Name.IsMissing);
        Assert.True(member.Name.Identifier.IsMissing);
        Assert.Equal(0, member.Name.Identifier.FullWidth);
        Assert.False(member.IsMissing);
    }

    [Fact]
    public void Logical_or_binds_looser_than_logical_and()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  x = a && b || c\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var assign = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(0));
        var or = Assert.IsType<BinaryExpressionSyntax>(assign.Right);
        Assert.Equal(SyntaxKind.BarBarToken, or.OperatorToken.Kind);
        var and = Assert.IsType<BinaryExpressionSyntax>(or.Left);
        Assert.Equal(SyntaxKind.AmpersandAmpersandToken, and.OperatorToken.Kind);
        Assert.Equal("a", ((IdentifierNameSyntax)((NameExpressionSyntax)and.Left).Name).Identifier.Text);
        Assert.Equal("b", ((IdentifierNameSyntax)((NameExpressionSyntax)and.Right).Name).Identifier.Text);
        Assert.Equal("c", ((IdentifierNameSyntax)((NameExpressionSyntax)or.Right).Name).Identifier.Text);
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
    public void Parse_guard_declaration_round_trips()
    {
        const string source = "ScriptName Foo\nGuard Mutex\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var guard = Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Mutex", guard.Name.Identifier.Text);
        Assert.False(guard.GuardKeyword.IsMissing);
        Assert.False(guard.Name.IsMissing);
    }

    [Fact]
    public void Parse_guard_declaration_doc_comment_round_trips()
    {
        const string source = "ScriptName Foo\n{A lock}\nGuard Mutex\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var guard = Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("{A lock}", guard.DocComment.Text);
        Assert.Equal("Mutex", guard.Name.Identifier.Text);
    }

    [Fact]
    public void Missing_guard_name_produces_missing_token_and_does_not_throw()
    {
        const string source = "ScriptName Foo\nGuard\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var guard = Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(0));
        Assert.True(guard.Name.IsMissing);
        Assert.Equal(0, guard.Name.Identifier.FullWidth);
    }

    [Fact]
    public void Skyrim_guard_is_an_identifier_not_a_declaration()
    {
        const string source = "ScriptName Foo\nGuard Mutex\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Guard", ((IdentifierNameSyntax)variable.TypeName.Name).Identifier.Text);
        Assert.Equal("Mutex", variable.Name.Identifier.Text);
    }

    [Fact]
    public void Fallout4_guard_is_an_identifier_not_a_declaration()
    {
        const string source = "ScriptName Foo\nGuard Mutex\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Fallout4);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Guard", ((IdentifierNameSyntax)variable.TypeName.Name).Identifier.Text);
        Assert.Equal("Mutex", variable.Name.Identifier.Text);
    }

    [Fact]
    public void Starfield_lock_is_an_identifier_not_a_keyword()
    {
        const string source = "ScriptName Foo\nLock Value\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Lock", ((IdentifierNameSyntax)variable.TypeName.Name).Identifier.Text);
        Assert.Equal("Value", variable.Name.Identifier.Text);
    }

    [Fact]
    public void Parse_guard_statement_round_trips()
    {
        const string source = "ScriptName Foo\nGuard Mutex\nFunction F()\nGuard Mutex\n  Return\nEndGuard\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(0));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        Assert.Equal(1, function.Statements.Count);
        var statement = Assert.IsType<GuardStatementSyntax>(function.Statements.Get(0));
        Assert.Equal(1, statement.LockParameters.Count);
        Assert.Equal("Mutex", statement.LockParameters.Get(0).Name.Identifier.Text);
        Assert.Equal(1, statement.Statements.Count);
        Assert.IsType<ReturnStatementSyntax>(statement.Statements.Get(0));
        Assert.False(statement.EndGuardKeyword.IsMissing);
        Assert.False(function.EndFunctionKeyword.IsMissing);
    }

    [Fact]
    public void Parse_try_guard_statement_round_trips()
    {
        const string source = "ScriptName Foo\nGuard A\nGuard B\nFunction F()\nTryGuard A, B\n  Return\nEndGuard\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(2));
        var statement = Assert.IsType<TryGuardStatementSyntax>(function.Statements.Get(0));
        Assert.Equal(2, statement.LockParameters.Count);
        Assert.Equal("A", statement.LockParameters.Get(0).Name.Identifier.Text);
        Assert.Equal("B", statement.LockParameters.Get(1).Name.Identifier.Text);
        Assert.Equal(SyntaxKind.CommaToken, statement.LockParameters.Get(1).CommaToken.Kind);
        Assert.False(statement.EndGuardKeyword.IsMissing);
    }

    [Fact]
    public void Parse_nested_guard_and_try_guard_round_trips()
    {
        const string source =
            "ScriptName Foo\nGuard A\nGuard B\nFunction F()\nGuard A\nTryGuard B\n  Return\nEndGuard\nEndGuard\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(2));
        var outer = Assert.IsType<GuardStatementSyntax>(function.Statements.Get(0));
        var inner = Assert.IsType<TryGuardStatementSyntax>(outer.Statements.Get(0));
        Assert.Equal("B", inner.LockParameters.Get(0).Name.Identifier.Text);
        Assert.False(inner.EndGuardKeyword.IsMissing);
        Assert.False(outer.EndGuardKeyword.IsMissing);
    }

    [Fact]
    public void Missing_end_guard_does_not_swallow_the_next_function()
    {
        const string source = "ScriptName Foo\nFunction F()\nGuard Mutex\n  Return\nFunction G()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.Equal(2, root.Members.Count);
        var first = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var statement = Assert.IsType<GuardStatementSyntax>(first.Statements.Get(0));
        Assert.True(statement.EndGuardKeyword.IsMissing);
        Assert.True(first.EndFunctionKeyword.IsMissing);
        var second = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        Assert.Equal("G", second.Name.Identifier.Text);
        Assert.False(second.EndFunctionKeyword.IsMissing);
    }

    [Fact]
    public void Parse_event_try_guard_round_trips()
    {
        const string source = "ScriptName Foo\nGuard Mutex\nEvent OnInit()\nTryGuard Mutex\nEndGuard\nEndEvent\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        var ev = Assert.IsType<EventDeclarationSyntax>(root.Members.Get(1));
        var statement = Assert.IsType<TryGuardStatementSyntax>(ev.Statements.Get(0));
        Assert.Equal("Mutex", statement.LockParameters.Get(0).Name.Identifier.Text);
        Assert.False(statement.EndGuardKeyword.IsMissing);
        Assert.False(ev.EndEventKeyword.IsMissing);
    }

    [Fact]
    public void Starfield_parses_fo4_constructs_and_guard()
    {
        const string source = "ScriptName A:Foo\nStruct Point\nInt X\nEndStruct\nGuard Mutex\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.IsType<QualifiedNameSyntax>(root.Name);
        Assert.Equal(2, root.Members.Count);
        Assert.IsType<StructDeclarationSyntax>(root.Members.Get(0));
        var guard = Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(1));
        Assert.Equal("Mutex", guard.Name.Identifier.Text);
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

    [Theory]
    [InlineData("ScriptName Foo\nGuard Mutex\n")]
    [InlineData("ScriptName Foo\nGuard A\nGuard B\nFunction F()\nGuard A, B\nEndGuard\nEndFunction\n")]
    [InlineData("ScriptName Foo\nGuard Mutex\nFunction F()\nTryGuard Mutex\n  Return\nEndGuard\nEndFunction\n")]
    [InlineData("ScriptName A:Foo\nLock Value\nGuard Mutex\nFunction F()\nGuard Mutex\nEndGuard\nEndFunction\n")]
    public void Well_formed_starfield_snippets_round_trip(string source)
    {
        var tree = SyntaxTree.Parse(source, PapyrusDialect.Starfield);

        Assert.Equal(source, tree.GetRoot().ToFullString());
        Assert.True(tree.TryGetRoot(out var root));
        Assert.False(root.IsMissing);
    }
}
