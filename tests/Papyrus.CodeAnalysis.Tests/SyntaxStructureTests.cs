using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class SyntaxStructureTests
{
    [Fact]
    public void Folds_functions_states_conditionals_and_comments()
    {
        const string source =
            "ScriptName Foo\n"
            + // 0
            "Import Utility\n"
            + // 1
            "Import Math\n"
            + // 2
            ";/ block\ncomment /;\n"
            + // 3-4
            "Function DoThing(Int a)\n"
            + // 5
            "  If a > 0\n"
            + // 6
            "    a = 1\n"
            + // 7
            "  Else\n"
            + // 8
            "    a = 2\n"
            + // 9
            "  EndIf\n"
            + // 10
            "EndFunction\n"
            + // 11
            "State Waiting\n"
            + // 12
            "EndState\n"; // 13
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var spans = SyntaxStructure.GetFoldingSpans(tree, TestContext.Current.CancellationToken);

        AssertContains(source, spans, "Import Utility", "Import Math\n", FoldingSpanKind.Imports);
        AssertContains(source, spans, ";/ block", "comment /;", FoldingSpanKind.Comment);
        AssertContains(source, spans, "Function DoThing", "EndFunction", FoldingSpanKind.Block);
        AssertContains(source, spans, "If a > 0", "EndIf", FoldingSpanKind.Block);
        AssertContains(source, spans, "Else\n", "a = 2", FoldingSpanKind.Block);
        AssertContains(source, spans, "State Waiting", "EndState", FoldingSpanKind.Block);

        // Sorted by start position.
        for (var i = 1; i < spans.Length; i++)
        {
            Assert.True(spans[i - 1].Span.Start <= spans[i].Span.Start);
        }
    }

    [Fact]
    public void Auto_properties_do_not_fold_but_full_properties_do()
    {
        const string source =
            "ScriptName Foo\n"
            + "Int Property Simple = 1 Auto\n"
            + "Int Property Full\n"
            + "  Int Function Get()\n"
            + "    Return 1\n"
            + "  EndFunction\n"
            + "EndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var spans = SyntaxStructure.GetFoldingSpans(tree, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            spans,
            s =>
                s.Span.Start
                == source.IndexOf("Int Property Simple", System.StringComparison.Ordinal)
        );
        AssertContains(source, spans, "Int Property Full", "EndProperty", FoldingSpanKind.Block);
        AssertContains(source, spans, "Int Function Get", "EndFunction", FoldingSpanKind.Block);
    }

    [Fact]
    public void Selection_spans_widen_from_token_to_ancestors()
    {
        const string source =
            "ScriptName Foo\n" + "Function DoThing(Int a)\n" + "  a = a + 1\n" + "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);
        var position = source.IndexOf("a + 1", System.StringComparison.Ordinal);

        var chain = SyntaxStructure.GetSelectionSpans(
            tree,
            position,
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(chain);

        // Innermost is the identifier token, then the binary expression, then
        // the assignment statement, then the function.
        Assert.Equal(new TextSpan(position, 1), chain[0]);
        Assert.Contains(chain, s => Matches(source, s, "a + 1"));

        // The assignment statement's span includes its end-of-line token.
        Assert.Contains(
            chain,
            s =>
                s.Start == source.IndexOf("a = a + 1", System.StringComparison.Ordinal)
                && s.End
                    >= source.IndexOf("a = a + 1", System.StringComparison.Ordinal)
                        + "a = a + 1".Length
        );
        Assert.Contains(
            chain,
            s => s.Start == source.IndexOf("Function DoThing", System.StringComparison.Ordinal)
        );

        // Strictly widening.
        for (var i = 1; i < chain.Length; i++)
        {
            Assert.True(chain[i].Start <= chain[i - 1].Start && chain[i].End >= chain[i - 1].End);
            Assert.True(chain[i].Length > chain[i - 1].Length);
        }
    }

    [Fact]
    public void Selection_spans_outside_the_tree_are_empty()
    {
        var tree = SyntaxTree.Parse("ScriptName Foo\n", PapyrusDialect.SkyrimSe);

        Assert.Empty(
            SyntaxStructure.GetSelectionSpans(tree, 1000, TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            SyntaxStructure.GetSelectionSpans(tree, -1, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void Document_symbols_are_structural_and_nested()
    {
        const string source =
            "ScriptName Foo\n"
            + "Int Property Value Auto\n"
            + "State Waiting\n"
            + "  Function DoThing()\n"
            + "  EndFunction\n"
            + "EndState\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var symbols = SyntaxStructure.GetDocumentSymbols(
            tree,
            TestContext.Current.CancellationToken
        );

        var script = Assert.Single(symbols);
        Assert.Equal("Foo", script.Name);
        Assert.Equal(DocumentSymbolKind.Class, script.Kind);
        Assert.Equal(
            new TextSpan(source.IndexOf("Foo", System.StringComparison.Ordinal), 3),
            script.SelectionSpan
        );

        var property = Assert.Single(script.Children, symbol => symbol.Name == "Value");
        Assert.Equal(DocumentSymbolKind.Property, property.Kind);
        var state = Assert.Single(script.Children, symbol => symbol.Name == "Waiting");
        var function = Assert.Single(state.Children);
        Assert.Equal("DoThing", function.Name);
        Assert.Equal(DocumentSymbolKind.Method, function.Kind);
    }

    [Fact]
    public void Syntax_tree_projection_preserves_root_text_and_tokens()
    {
        const string source = "ScriptName Foo\nFunction Go()\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var projection = SyntaxStructure.GetSyntaxTree(tree);

        Assert.Equal(SyntaxKind.ScriptDeclaration, projection.Kind);
        Assert.Equal(source, projection.Text);
        Assert.False(projection.IsToken);
        Assert.Contains(
            projection.Children.SelectMany(DescendantsAndSelf),
            node =>
                node.IsToken
                && node.Kind == SyntaxKind.ScriptNameKeyword
                && node.Text == "ScriptName"
        );
    }

    [Fact]
    public void Indentation_edits_are_source_preserving_and_idempotent()
    {
        const string source =
            "ScriptName Foo\n"
            + "Function Go()\n"
            + "  If true\n"
            + "x = 1\n"
            + "  EndIf\n"
            + "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var formatted = SyntaxFormatter.ApplyIndentation(tree);
        Assert.Equal(
            "ScriptName Foo\n"
                + "Function Go()\n"
                + "    If true\n"
                + "        x = 1\n"
                + "    EndIf\n"
                + "EndFunction\n",
            formatted
        );
        Assert.Equal(
            formatted,
            SyntaxFormatter.ApplyIndentation(SyntaxTree.Parse(formatted, PapyrusDialect.SkyrimSe))
        );
        Assert.Contains("If true", formatted);
        Assert.Contains("x = 1", formatted);
    }

    [Fact]
    public void Indentation_keeps_doc_comments_level_with_the_header_they_document()
    {
        const string source =
            "ScriptName Foo\n"
            + "Function Go()\n"
            + "{Does the thing}\n"
            + "x = 1\n"
            + "EndFunction\n"
            + "Group Settings\n"
            + "  {The settings}\n"
            + "Int Property A Auto\n"
            + "{A}\n"
            + "EndGroup\n";

        var formatted = SyntaxFormatter.ApplyIndentation(SyntaxTree.Parse(source, PapyrusDialect.Fallout4));

        Assert.Equal(
            "ScriptName Foo\n"
                + "Function Go()\n"
                + "{Does the thing}\n"
                + "    x = 1\n"
                + "EndFunction\n"
                + "Group Settings\n"
                + "{The settings}\n"
                + "    Int Property A Auto\n"
                + "    {A}\n"
                + "EndGroup\n",
            formatted
        );
    }

    [Fact]
    public void Indentation_handles_typed_function_and_property_declarations()
    {
        const string source =
            "ScriptName Foo\n"
            + "Int Property Value\n"
            + "  Int Function Get()\n"
            + "  Return 1\n"
            + "  EndFunction\n"
            + "EndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var formatted = SyntaxFormatter.ApplyIndentation(tree);

        Assert.Contains("    Int Function Get()\n", formatted);
        Assert.Contains("        Return 1\n", formatted);
        Assert.Contains("    EndFunction\n", formatted);
    }

    [Fact]
    public void Indentation_ignores_keywords_in_comments_strings_and_bodiless_declarations()
    {
        const string source =
            "ScriptName Foo\n"
            + "Int Property Count Auto\n"
            + "Function Native() Native\n"
            + "; this function does nothing\n"
            + "Auto State Ready\n"
            + "Event OnInit()\n"
            + "Debug.Trace(\"my event fired\")\n"
            + "if(Count)\n"
            + "Count = 1\n"
            + "endif\n"
            + "EndEvent\n"
            + "EndState\n";

        var formatted = SyntaxFormatter.ApplyIndentation(SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe));

        Assert.Equal(
            "ScriptName Foo\n"
                + "Int Property Count Auto\n"
                + "Function Native() Native\n"
                + "; this function does nothing\n"
                + "Auto State Ready\n"
                + "    Event OnInit()\n"
                + "        Debug.Trace(\"my event fired\")\n"
                + "        if(Count)\n"
                + "            Count = 1\n"
                + "        endif\n"
                + "    EndEvent\n"
                + "EndState\n",
            formatted
        );
    }

    [Fact]
    public void Indentation_indents_comments_on_their_own_lines_and_keeps_inner_lines_of_block_comments()
    {
        const string source =
            "ScriptName Foo\n"
            + "Function Go(Int a, \\\n"
            + "            Int b)\n"
            + "\n"
            + ";first\n"
            + ";/ block\n"
            + "   stays put /;\n"
            + "a = b\n"
            + ";last\n"
            + "EndFunction\n";

        var formatted = SyntaxFormatter.ApplyIndentation(SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe));

        Assert.Equal(
            "ScriptName Foo\n"
                + "Function Go(Int a, \\\n"
                + "            Int b)\n"
                + "\n"
                + "    ;first\n"
                + "    ;/ block\n"
                + "   stays put /;\n"
                + "    a = b\n"
                + "    ;last\n"
                + "EndFunction\n",
            formatted
        );
    }

    [Fact]
    public void Indentation_edits_can_be_limited_to_a_span()
    {
        const string source = "ScriptName Foo\nFunction Go()\nInt a\nInt b\nEndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var edits = SyntaxFormatter.GetIndentationEdits(tree, new TextSpan(source.IndexOf("Int b", System.StringComparison.Ordinal), 0));

        var edit = Assert.Single(edits);
        Assert.Equal(source.IndexOf("Int b", System.StringComparison.Ordinal), edit.Span.Start);
        Assert.Equal("    ", edit.NewText);
    }

    [Theory]
    [InlineData("    EndIf", true)]
    [InlineData("Else", true)]
    [InlineData("ElseIf x", true)]
    [InlineData("EndFunction ; done", true)]
    [InlineData("Elsewhere = 1", false)]
    [InlineData("If x", false)]
    public void Block_keyword_lines_are_detected(string line, bool expected)
    {
        var tree = SyntaxTree.Parse("ScriptName Foo\n" + line + "\n", PapyrusDialect.SkyrimSe);

        Assert.Equal(expected, SyntaxFormatter.StartsWithBlockKeyword(tree, 1));
    }

    private static void AssertContains(
        string source,
        ImmutableArray<FoldingSpan> spans,
        string startAnchor,
        string endAnchor,
        FoldingSpanKind kind
    )
    {
        var start = source.IndexOf(startAnchor, System.StringComparison.Ordinal);
        Assert.True(start >= 0, $"start anchor not found: {startAnchor}");
        var endAnchorStart = source.IndexOf(endAnchor, start, System.StringComparison.Ordinal);
        Assert.True(endAnchorStart >= 0, $"end anchor not found: {endAnchor}");

        var matches = spans.Where(s => s.Span.Start == start && s.Kind == kind).ToArray();
        Assert.True(
            matches.Length == 1,
            $"expected one {kind} span at '{startAnchor}', found {matches.Length}"
        );
        var span = matches[0].Span;
        Assert.True(
            span.End >= endAnchorStart && span.End <= endAnchorStart + endAnchor.Length + 1,
            $"span {span} does not end at '{endAnchor}' ({endAnchorStart}..{endAnchorStart + endAnchor.Length})"
        );
    }

    private static bool Matches(string source, TextSpan span, string anchor)
    {
        var start = source.IndexOf(anchor, System.StringComparison.Ordinal);
        return span.Start == start && span.Length == anchor.Length;
    }

    private static IEnumerable<SyntaxTreeNode> DescendantsAndSelf(SyntaxTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }
}
