using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.LanguageServer;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class StructureHandlerTests
{
    private const string Script =
        "ScriptName Foo\n" +          // 0
        "Function DoThing(Int a)\n" + // 1
        "  If a > 0\n" +              // 2
        "    a = 1\n" +               // 3
        "  EndIf\n" +                 // 4
        "EndFunction\n";              // 5

    [Fact]
    public async Task Folding_returns_line_ranges_for_blocks()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(Script, timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new FoldingRangeHandler(),
            new FoldingRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token);

        Assert.Contains(result, r => r is { StartLine: 1, EndLine: 5, Kind: null });
        Assert.Contains(result, r => r is { StartLine: 2, EndLine: 4, Kind: null });
        Assert.All(result, r => Assert.True(r.EndLine > r.StartLine));
    }

    [Fact]
    public async Task Selection_ranges_link_widening_parents_per_position()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(Script, timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new SelectionRangeHandler(),
            new SelectionRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Positions =
                [
                    new Position { Line = 3, Character = 4 },   // the 'a' in 'a = 1'
                    new Position { Line = 0, Character = 0 },   // 'ScriptName'
                ],
            },
            timeout.Token);

        Assert.Equal(2, result.Length);

        var first = result[0];
        Assert.Equal(3, first.Range.Start?.Line);
        Assert.Equal(4, first.Range.Start?.Character);
        Assert.Equal(5, first.Range.End?.Character);   // the identifier token 'a'

        // Walk the parent chain; each parent strictly contains its child.
        var depth = 0;
        for (var current = first; current.Parent is not null; current = current.Parent)
        {
            depth++;
            Assert.True(Contains(current.Parent.Range, current.Range));
        }

        Assert.True(depth >= 2);

        Assert.Equal(0, result[1].Range.Start?.Line);
    }

    [Fact]
    public async Task Unknown_document_returns_empty_results()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(Script, timeout.Token);
        var missing = new TextDocumentIdentifier
        {
            Uri = LspTestWorkspace.ToFileUri(Path.Combine(fixture.Root, "scripts", "Missing.psc")),
        };

        var folding = await fixture.Scheduler.ExecuteAsync(
            new FoldingRangeHandler(),
            new FoldingRangeParams { TextDocument = missing },
            timeout.Token);
        var selection = await fixture.Scheduler.ExecuteAsync(
            new SelectionRangeHandler(),
            new SelectionRangeParams
            {
                TextDocument = missing,
                Positions = [new Position()],
            },
            timeout.Token);

        Assert.Empty(folding);
        Assert.Empty(selection);
    }

    [Fact]
    public async Task Document_symbols_return_hierarchical_ranges()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nInt Property Value Auto\nState Waiting\n  Function DoThing()\n  EndFunction\nEndState\n",
            timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new DocumentSymbolHandler(),
            new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token);

        var script = Assert.Single(result);
        Assert.Equal("Foo", script.Name);
        Assert.Equal(5, script.Kind);
        Assert.Contains(script.Children, symbol => symbol.Name == "Value" && symbol.Kind == 7);
        var state = Assert.Single(script.Children, symbol => symbol.Name == "Waiting");
        var function = Assert.Single(state.Children);
        Assert.Equal("DoThing", function.Name);
        Assert.Equal(6, function.Kind);
        Assert.True(function.SelectionRange.Start!.Line >= state.Range.Start!.Line);
    }

    [Fact]
    public async Task Syntax_tree_returns_root_and_token_nodes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(Script, timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new DocumentSyntaxTreeHandler(),
            new DocumentSyntaxTreeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token);

        Assert.NotNull(result?.Root);
        Assert.Equal("ScriptDeclaration", result!.Root!.Kind);
        Assert.Contains(
            DescendantsAndSelf(result.Root),
            node => node.IsToken && node.Kind == "ScriptNameKeyword" && node.Text == "ScriptName");
    }

    [Fact]
    public async Task Formatting_returns_leading_indentation_edits()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync("ScriptName Foo\nFunction Go()\nx = 1\nEndFunction\n", timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new FormattingHandler(),
            new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Options = new FormattingOptions { TabSize = 4, InsertSpaces = true },
            },
            timeout.Token);

        var edit = Assert.Single(result);
        Assert.Equal(2, edit.Range.Start!.Line);
        Assert.Equal(4, edit.NewText.Length);
        Assert.Equal("    ", edit.NewText);
    }

    private static bool Contains(Range outer, Range inner)
    {
        var startsBefore = outer.Start!.Line < inner.Start!.Line
            || (outer.Start.Line == inner.Start.Line && outer.Start.Character <= inner.Start.Character);
        var endsAfter = outer.End!.Line > inner.End!.Line
            || (outer.End.Line == inner.End.Line && outer.End.Character >= inner.End.Character);
        return startsBefore && endsAfter;
    }

    private static System.Collections.Generic.IEnumerable<SyntaxTreeNode> DescendantsAndSelf(SyntaxTreeNode node)
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
