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
            "ScriptName Foo\n" +                     // 0
            "Import Utility\n" +                     // 1
            "Import Math\n" +                        // 2
            ";/ block\ncomment /;\n" +               // 3-4
            "Function DoThing(Int a)\n" +            // 5
            "  If a > 0\n" +                         // 6
            "    a = 1\n" +                          // 7
            "  Else\n" +                             // 8
            "    a = 2\n" +                          // 9
            "  EndIf\n" +                            // 10
            "EndFunction\n" +                        // 11
            "State Waiting\n" +                      // 12
            "EndState\n";                            // 13
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
            "ScriptName Foo\n" +
            "Int Property Simple = 1 Auto\n" +
            "Int Property Full\n" +
            "  Int Function Get()\n" +
            "    Return 1\n" +
            "  EndFunction\n" +
            "EndProperty\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);

        var spans = SyntaxStructure.GetFoldingSpans(tree, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            spans,
            s => s.Span.Start == source.IndexOf("Int Property Simple", System.StringComparison.Ordinal));
        AssertContains(source, spans, "Int Property Full", "EndProperty", FoldingSpanKind.Block);
        AssertContains(source, spans, "Int Function Get", "EndFunction", FoldingSpanKind.Block);
    }

    [Fact]
    public void Selection_spans_widen_from_token_to_ancestors()
    {
        const string source =
            "ScriptName Foo\n" +
            "Function DoThing(Int a)\n" +
            "  a = a + 1\n" +
            "EndFunction\n";
        var tree = SyntaxTree.Parse(source, PapyrusDialect.SkyrimSe);
        var position = source.IndexOf("a + 1", System.StringComparison.Ordinal);

        var chain = SyntaxStructure.GetSelectionSpans(tree, position, TestContext.Current.CancellationToken);

        Assert.NotEmpty(chain);

        // Innermost is the identifier token, then the binary expression, then
        // the assignment statement, then the function.
        Assert.Equal(new TextSpan(position, 1), chain[0]);
        Assert.Contains(chain, s => Matches(source, s, "a + 1"));

        // The assignment statement's span includes its end-of-line token.
        Assert.Contains(
            chain,
            s => s.Start == source.IndexOf("a = a + 1", System.StringComparison.Ordinal)
                && s.End >= source.IndexOf("a = a + 1", System.StringComparison.Ordinal) + "a = a + 1".Length);
        Assert.Contains(
            chain,
            s => s.Start == source.IndexOf("Function DoThing", System.StringComparison.Ordinal));

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

        Assert.Empty(SyntaxStructure.GetSelectionSpans(tree, 1000, TestContext.Current.CancellationToken));
        Assert.Empty(SyntaxStructure.GetSelectionSpans(tree, -1, TestContext.Current.CancellationToken));
    }

    private static void AssertContains(
        string source,
        ImmutableArray<FoldingSpan> spans,
        string startAnchor,
        string endAnchor,
        FoldingSpanKind kind)
    {
        var start = source.IndexOf(startAnchor, System.StringComparison.Ordinal);
        Assert.True(start >= 0, $"start anchor not found: {startAnchor}");
        var endAnchorStart = source.IndexOf(endAnchor, start, System.StringComparison.Ordinal);
        Assert.True(endAnchorStart >= 0, $"end anchor not found: {endAnchor}");

        var matches = spans.Where(s => s.Span.Start == start && s.Kind == kind).ToArray();
        Assert.True(matches.Length == 1, $"expected one {kind} span at '{startAnchor}', found {matches.Length}");
        var span = matches[0].Span;
        Assert.True(
            span.End >= endAnchorStart && span.End <= endAnchorStart + endAnchor.Length + 1,
            $"span {span} does not end at '{endAnchor}' ({endAnchorStart}..{endAnchorStart + endAnchor.Length})");
    }

    private static bool Matches(string source, TextSpan span, string anchor)
    {
        var start = source.IndexOf(anchor, System.StringComparison.Ordinal);
        return span.Start == start && span.Length == anchor.Length;
    }
}
