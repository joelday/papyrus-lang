using System;
using System.Collections.Generic;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SemanticModelTests
{
    [Fact]
    public void GetSemanticModel_null_tree_throws()
    {
        var compilation = Compilation(PapyrusDialect.SkyrimSe, "Foo", "ScriptName Foo\n");
        Assert.Throws<ArgumentNullException>(() => compilation.GetSemanticModel(null!));
    }

    [Fact]
    public void GetDeclaredSymbol_function_and_property()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nInt Property Health Auto\nFunction Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));

        var property = Assert.IsType<PropertyDeclarationSyntax>(root.Members.Get(0));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));

        var propertySymbol = Assert.IsAssignableFrom<IPropertySymbol>(
            model.GetDeclaredSymbol(property, TestContext.Current.CancellationToken)
        );
        Assert.Equal("Health", propertySymbol.Name.Text);
        Assert.Equal("Int", propertySymbol.Type.Name.Text);

        var functionSymbol = Assert.IsAssignableFrom<IFunctionSymbol>(
            model.GetDeclaredSymbol(function, TestContext.Current.CancellationToken)
        );
        Assert.Equal("Bar", functionSymbol.Name.Text);
        Assert.Same(
            functionSymbol,
            model.GetDeclaredSymbol(function.Name, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void GetDeclaredSymbol_variable_parameter_state_struct_and_guard()
    {
        var compilation = Compilation(
            PapyrusDialect.Starfield,
            "Foo",
            "ScriptName Foo\n"
                + "Int Count = 1\n"
                + "Function Mix(Float value)\nEndFunction\n"
                + "State Ready\nFunction Swing()\nEndFunction\nEndState\n"
                + "Struct Point\nFloat X\nEndStruct\n"
                + "Guard Mutex\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));

        var token = TestContext.Current.CancellationToken;
        var variable = Assert.IsType<VariableDeclarationSyntax>(root.Members.Get(0));
        Assert.Equal("Count", model.GetDeclaredSymbol(variable, token)?.Name.Text);

        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(1));
        var parameter = function.ParameterList.Parameters.Get(0);
        var parameterSymbol = Assert.IsAssignableFrom<IParameterSymbol>(
            model.GetDeclaredSymbol(parameter, token)
        );
        Assert.Equal("value", parameterSymbol.Name.Text);
        Assert.Equal("Float", parameterSymbol.Type.Name.Text);

        var state = Assert.IsType<StateDeclarationSyntax>(root.Members.Get(2));
        Assert.IsAssignableFrom<IStateSymbol>(model.GetDeclaredSymbol(state, token));

        var struc = Assert.IsType<StructDeclarationSyntax>(root.Members.Get(3));
        Assert.IsAssignableFrom<IStructSymbol>(model.GetDeclaredSymbol(struc, token));

        var guard = Assert.IsType<GuardDeclarationSyntax>(root.Members.Get(4));
        Assert.IsAssignableFrom<IGuardSymbol>(model.GetDeclaredSymbol(guard, token));

        Assert.Equal("Foo", model.GetDeclaredSymbol(root, token)?.Name.Text);
    }

    [Fact]
    public void GetTypeInfo_int_literal_and_as_float()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Int x = 1\n  Float y = x As Float\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);

        var intType = model.GetTypeInfo(
            Initializer(tree, 0),
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Int", intType?.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, intType?.TypeKind);

        var asFloat = model.GetTypeInfo(
            Initializer(tree, 1),
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Float", asFloat?.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, asFloat?.TypeKind);
    }

    [Fact]
    public void LookupSymbols_finds_Bar()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));

        var symbols = model.LookupSymbols(function.Span.Start);
        var bar = Assert.Single(symbols);
        Assert.Equal("Bar", bar.Name.Text);
        Assert.Equal(SymbolKind.Function, bar.Kind);

        var script = compilation.GetScript(QualifiedName.Parse("Foo"));
        var fromContainer = model.LookupSymbols(function.Span.Start, script);
        Assert.Equal("Bar", Assert.Single(fromContainer).Name.Text);
    }

    [Fact]
    public void LookupSymbols_at_position_includes_locals_parameters_and_base_members()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo Extends Base\nFunction Bar(Int a)\n  Int b = 1\nEndFunction\n",
            ("Base", "ScriptName Base\nFunction Inherited()\nEndFunction\n")
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(function.Statements.Get(0));

        var symbols = model.LookupSymbols(declare.Span.Start);
        var names = symbols.Select(s => s.Name.Text).ToArray();

        Assert.Contains("a", names);
        Assert.Contains("Bar", names);
        Assert.Contains("Inherited", names);
    }

    [Fact]
    public void LookupSymbols_with_container_walks_base_type_chain()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo Extends Base\nFunction Bar()\nEndFunction\n",
            ("Base", "ScriptName Base\nFunction Inherited()\nEndFunction\n")
        );
        var script = compilation.GetScript(QualifiedName.Parse("Foo"));
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);

        var names = model.LookupSymbols(0, script).Select(s => s.Name.Text).ToArray();

        Assert.Contains("Bar", names);
        Assert.Contains("Inherited", names);
    }

    [Fact]
    public void TryGetMemberAccessContainer_resolves_type_before_dot()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\nEndFunction\n"
                + "Function Baz()\n  Foo x\n  x.Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var source = tree.GetText().ToString();
        var dotPosition = source.IndexOf("x.Bar", StringComparison.Ordinal) + 1;

        var found = model.TryGetMemberAccessContainer(
            dotPosition,
            out var container,
            TestContext.Current.CancellationToken
        );

        Assert.True(found);
        Assert.Equal("Foo", container?.Name.Text);
    }

    [Fact]
    public void TryGetMemberAccessContainer_outside_member_access_returns_false()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));

        var found = model.TryGetMemberAccessContainer(
            function.Span.Start,
            out var container,
            TestContext.Current.CancellationToken
        );

        Assert.False(found);
        Assert.Null(container);
    }

    [Fact]
    public void TryGetInvocationContext_reports_function_and_active_parameter()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar(Int a, Int b)\nEndFunction\n"
                + "Function Baz()\n  Bar(1, 2)\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var source = tree.GetText().ToString();
        var secondArgumentPosition = source.IndexOf("2)", StringComparison.Ordinal);

        var found = model.TryGetInvocationContext(
            secondArgumentPosition,
            out var function,
            out var activeParameterIndex,
            TestContext.Current.CancellationToken
        );

        Assert.True(found);
        Assert.Equal("Bar", function?.Name.Text);
        Assert.Equal(1, activeParameterIndex);
    }

    [Fact]
    public void TryGetSpeculativeSemanticModel_types_addition_without_changing_tree()
    {
        const string source = "ScriptName Foo\nFunction Bar()\n  Int x = 1\nEndFunction\n";
        var compilation = Compilation(PapyrusDialect.SkyrimSe, "Foo", source);
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var originalText = tree.GetText().ToString();
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));

        var dummy = SyntaxTree.Parse(
            "ScriptName Dummy\nFunction F()\n  Int z = 1 + 2\nEndFunction\n",
            PapyrusDialect.SkyrimSe
        );
        var addition = Initializer(dummy, 0);

        Assert.True(
            model.TryGetSpeculativeSemanticModel(
                function.Statements.Get(0).Span.Start,
                addition,
                out var speculative
            )
        );
        Assert.NotSame(model, speculative);
        Assert.Same(compilation, speculative.Compilation);
        Assert.Same(tree, speculative.SyntaxTree);

        var type = speculative.GetTypeInfo(addition, TestContext.Current.CancellationToken);
        Assert.Equal("Int", type?.Name.Text);
        Assert.Equal(TypeKind.Intrinsic, type?.TypeKind);

        Assert.Same(tree, compilation.GetSyntaxTree(QualifiedName.Parse("Foo")));
        Assert.Equal(originalText, tree.GetText().ToString());
        Assert.Equal(source, originalText);
        Assert.NotSame(tree, addition.SyntaxTree);
    }

    [Fact]
    public void GetTypeInfo_skyrim_is_is_bool_and_classify_still_reports()
    {
        var parsed = Compilation(
            PapyrusDialect.Fallout4,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\n  Bool x = None is Parent\nEndFunction\n",
            ("Parent", "ScriptName Parent\n")
        );
        var skyrim = Compilation(
            PapyrusDialect.SkyrimSe,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\nEndFunction\n",
            ("Parent", "ScriptName Parent\n")
        );
        var isExpression = Initializer(parsed.GetSyntaxTree(QualifiedName.Parse("Child")), 0);
        var model = skyrim.GetSemanticModel(skyrim.GetSyntaxTree(QualifiedName.Parse("Child")));
        Assert.True(skyrim.GetSyntaxTree(QualifiedName.Parse("Child")).TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));

        Assert.True(
            model.TryGetSpeculativeSemanticModel(
                function.Span.End,
                isExpression,
                out var speculative
            )
        );
        var type = speculative.GetTypeInfo(isExpression, TestContext.Current.CancellationToken);
        Assert.Equal("Bool", type?.Name.Text);

        var names = skyrim.Names;
        var result = TypeConversions.ClassifyIs(
            skyrim.GetScript(QualifiedName.Parse("Child")),
            skyrim.GetScript(QualifiedName.Parse("Parent")),
            PapyrusDialect.SkyrimSe,
            names
        );
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, result.Diagnostic?.Id);
        Assert.Equal("Bool", result.ResultType.Name.Text);

        var bound = new ExpressionTypeBinder(
            skyrim,
            skyrim.GetScript(QualifiedName.Parse("Child"))
        ).Bind(isExpression);
        Assert.Equal(PapyrusCompilation.InvalidIsOperatorDiagnosticId, bound.Diagnostic?.Id);
        Assert.Equal("Bool", bound.ResultType.Name.Text);
    }

    [Fact]
    public void GetSymbolInfo_identifier_member_and_extends()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Child",
            "ScriptName Child Extends Parent\nFunction Bar()\n  Health = 1\nEndFunction\n",
            ("Parent", "ScriptName Parent\nInt Property Health Auto\n")
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Child"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));

        Assert.NotNull(root.ExtendsClause);
        var extends = root.ExtendsClause!;
        var baseSymbol = Assert.IsAssignableFrom<IScriptSymbol>(
            model.GetSymbolInfo(extends.TypeName.Name, TestContext.Current.CancellationToken)
        );
        Assert.Equal("Parent", baseSymbol.Name.Text);
        Assert.Same(compilation.GetScript(QualifiedName.Parse("Parent")), baseSymbol);

        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var assign = Assert.IsType<AssignmentStatementSyntax>(function.Statements.Get(0));
        var health = Assert.IsAssignableFrom<IPropertySymbol>(
            model.GetSymbolInfo(assign.Left, TestContext.Current.CancellationToken)
        );
        Assert.Equal("Health", health.Name.Text);
        Assert.Same(
            compilation
                .GetScript(QualifiedName.Parse("Parent"))
                .GetMembers(compilation.Names.Get("Health"))[0],
            health
        );
    }

    [Fact]
    public void GetSymbolInfo_by_position_resolves_identifier()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var position = tree.GetText().ToString().LastIndexOf("Bar()", StringComparison.Ordinal);

        var symbol = model.GetSymbolInfo(position, TestContext.Current.CancellationToken);

        Assert.Equal("Bar", symbol?.Name.Text);
        Assert.Equal(SymbolKind.Function, symbol?.Kind);
    }

    [Fact]
    public void Source_symbol_exposes_declaration_reference()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var symbol = model.GetSymbolInfo(
            tree.GetText().ToString().IndexOf("Bar", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var reference = Assert.Single(symbol!.DeclaringSyntaxReferences);
        Assert.Same(tree, reference.SyntaxTree);
        Assert.Equal("Function Bar()\nEndFunction", tree.GetText().ToString(reference.Span));
    }

    [Fact]
    public void FindReferencesInTree_finds_declaration_and_all_call_sites()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\n"
                + "Function Bar()\n"
                + "  Bar()\n"
                + "EndFunction\n"
                + "Function Baz()\n"
                + "  Bar()\n"
                + "EndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var symbol = Assert.IsAssignableFrom<IFunctionSymbol>(
            model.GetDeclaredSymbol(function, TestContext.Current.CancellationToken)
        );

        var spans = model.FindReferencesInTree(symbol, TestContext.Current.CancellationToken);

        Assert.Equal(3, spans.Length);
        Assert.Equal(1, spans.Count(s => s.IsDeclaration));
        var source = tree.GetText();
        Assert.All(spans, span => Assert.Equal("Bar", source.ToString(span.Span)));
    }

    [Fact]
    public void FindReferencesInTree_excludes_unrelated_symbols()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\nEndFunction\nFunction Baz()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        Assert.True(tree.TryGetRoot(out var root));
        var bar = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var barSymbol = model.GetDeclaredSymbol(bar, TestContext.Current.CancellationToken)!;

        var spans = model.FindReferencesInTree(
            barSymbol,
            TestContext.Current.CancellationToken
        );

        var source = tree.GetText();
        Assert.All(spans, span => Assert.Equal("Bar", source.ToString(span.Span)));
        Assert.DoesNotContain(spans, span => source.ToString(span.Span) == "Baz");
    }

    [Fact]
    public void TryGetRenameInfo_resolves_identifier_span_and_symbol()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var offset = tree.GetText().ToString().IndexOf("Bar()\nEndFunction", StringComparison.Ordinal);

        var result = model.TryGetRenameInfo(
            offset,
            TestContext.Current.CancellationToken,
            out var span,
            out var symbol
        );

        Assert.True(result);
        Assert.Equal("Bar", tree.GetText().ToString(span));
        Assert.Equal("Bar", symbol.Name.Text);
    }

    [Fact]
    public void TryGetRenameInfo_rejects_native_symbols()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo\nFunction Bar() Native\nFunction Baz()\n  Bar()\nEndFunction\n"
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var offset = tree.GetText().ToString().IndexOf("Bar()\nEndFunction", StringComparison.Ordinal);

        var result = model.TryGetRenameInfo(
            offset,
            TestContext.Current.CancellationToken,
            out _,
            out _
        );

        Assert.False(result);
    }

    [Fact]
    public void TryGetRenameInfo_rejects_script_symbols()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo Extends Base\n",
            ("Base", "ScriptName Base\n")
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);
        var offset = tree.GetText().ToString().IndexOf("Base", StringComparison.Ordinal);

        var result = model.TryGetRenameInfo(
            offset,
            TestContext.Current.CancellationToken,
            out _,
            out _
        );

        Assert.False(result);
    }

    [Fact]
    public void DeclaresMemberDirectly_distinguishes_own_from_inherited_events()
    {
        var compilation = Compilation(
            PapyrusDialect.SkyrimSe,
            "Foo",
            "ScriptName Foo Extends Base\nEvent OnInit()\nEndEvent\n",
            ("Base", "ScriptName Base\nEvent OnInit()\nEndEvent\nEvent OnLoad()\nEndEvent\n")
        );
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);

        Assert.True(model.DeclaresMemberDirectly(new PapyrusName("OnInit"), SymbolKind.Event));
        Assert.False(model.DeclaresMemberDirectly(new PapyrusName("OnLoad"), SymbolKind.Event));
    }

    [Fact]
    public void TryGetSpeculativeSemanticModel_null_returns_false()
    {
        var compilation = Compilation(PapyrusDialect.SkyrimSe, "Foo", "ScriptName Foo\n");
        var tree = compilation.GetSyntaxTree(QualifiedName.Parse("Foo"));
        var model = compilation.GetSemanticModel(tree);

        Assert.False(model.TryGetSpeculativeSemanticModel(0, null!, out var speculative));
        Assert.Null(speculative);
    }

    private static ExpressionSyntax Initializer(SyntaxTree tree, int statementIndex)
    {
        Assert.True(tree.TryGetRoot(out var root));
        var function = Assert.IsType<FunctionDeclarationSyntax>(root.Members.Get(0));
        var declare = Assert.IsType<DeclareStatementSyntax>(
            function.Statements.Get(statementIndex)
        );
        Assert.NotNull(declare.EqualsValue);
        return declare.EqualsValue.Value;
    }

    private static PapyrusCompilation Compilation(
        PapyrusDialect dialect,
        string name,
        string source,
        params (string Name, string Source)[] extras
    )
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
