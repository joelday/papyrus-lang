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
        "ScriptName Foo\n"
        + // 0
        "Function DoThing(Int a)\n"
        + // 1
        "  If a > 0\n"
        + // 2
        "    a = 1\n"
        + // 3
        "  EndIf\n"
        + // 4
        "EndFunction\n"; // 5

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
            timeout.Token
        );

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
                    new Position { Line = 3, Character = 4 }, // the 'a' in 'a = 1'
                    new Position { Line = 0, Character = 0 }, // 'ScriptName'
                ],
            },
            timeout.Token
        );

        Assert.Equal(2, result.Length);

        var first = result[0];
        Assert.Equal(3, first.Range.Start?.Line);
        Assert.Equal(4, first.Range.Start?.Character);
        Assert.Equal(5, first.Range.End?.Character); // the identifier token 'a'

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
            timeout.Token
        );
        var selection = await fixture.Scheduler.ExecuteAsync(
            new SelectionRangeHandler(),
            new SelectionRangeParams { TextDocument = missing, Positions = [new Position()] },
            timeout.Token
        );

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
            timeout.Token
        );

        var result = await fixture.Scheduler.ExecuteAsync(
            new DocumentSymbolHandler(),
            new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token
        );

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
            timeout.Token
        );

        Assert.NotNull(result?.Root);
        Assert.Equal("ScriptDeclaration", result!.Root!.Kind);
        Assert.Contains(
            DescendantsAndSelf(result.Root),
            node => node.IsToken && node.Kind == "ScriptNameKeyword" && node.Text == "ScriptName"
        );
    }

    [Fact]
    public async Task Formatting_returns_leading_indentation_edits()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Go()\nx = 1\nEndFunction\n",
            timeout.Token
        );

        var result = await fixture.Scheduler.ExecuteAsync(
            new FormattingHandler(),
            new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Options = new FormattingOptions { TabSize = 4, InsertSpaces = true },
            },
            timeout.Token
        );

        var edit = Assert.Single(result);
        Assert.Equal(2, edit.Range.Start!.Line);
        Assert.Equal(4, edit.NewText.Length);
        Assert.Equal("    ", edit.NewText);
    }

    [Fact]
    public async Task Hover_and_definition_use_new_semantic_model()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var hover = await fixture.Scheduler.ExecuteAsync(
            new HoverHandler(),
            new TextDocumentPositionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 2, Character = 3 },
            },
            timeout.Token
        );
        var definition = await fixture.Scheduler.ExecuteAsync(
            new DefinitionHandler(),
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 2, Character = 3 },
            },
            timeout.Token
        );

        Assert.Contains(
            hover?.Contents ?? [],
            content => content.Contains("Bar", StringComparison.Ordinal)
        );
        var location = Assert.Single(definition);
        Assert.Equal(fixture.ScriptUri, location.Uri);
        Assert.Equal(1, location.Range.Start!.Line);
    }

    [Fact]
    public async Task Completion_lists_locals_parameters_and_script_members_in_scope()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\n"
                + "Function Bar(Int a)\n"
                + "  Int b = 1\n"
                + "  \n"
                + "EndFunction\n"
                + "Function Baz()\n"
                + "EndFunction\n",
            timeout.Token
        );

        var completions = await fixture.Scheduler.ExecuteAsync(
            new CompletionHandler(),
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 3, Character = 0 },
            },
            timeout.Token
        );

        var labels = completions.Select(item => item.Label).ToArray();
        Assert.Contains("a", labels);
        Assert.Contains("b", labels);
        Assert.Contains("Bar", labels);
        Assert.Contains("Baz", labels);
    }

    [Fact]
    public async Task Completion_after_dot_lists_container_members()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\n"
                + "Function Bar()\n"
                + "EndFunction\n"
                + "Function Baz()\n"
                + "  Foo x\n"
                + "  x.Bar()\n"
                + "EndFunction\n",
            timeout.Token
        );

        var completions = await fixture.Scheduler.ExecuteAsync(
            new CompletionHandler(),
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 5, Character = 5 },
            },
            timeout.Token
        );

        Assert.Contains(completions, item => item.Label == "Bar");
    }

    [Fact]
    public async Task SignatureHelp_reports_active_parameter()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\n"
                + "Function Bar(Int a, Int b)\n"
                + "EndFunction\n"
                + "Function Baz()\n"
                + "  Bar(1, )\n"
                + "EndFunction\n",
            timeout.Token
        );

        var help = await fixture.Scheduler.ExecuteAsync(
            new SignatureHelpHandler(),
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 4, Character = 9 },
            },
            timeout.Token
        );

        var signature = Assert.Single(help?.Signatures ?? []);
        Assert.Equal("Bar(Int a, Int b)", signature.Label);
        Assert.Equal(1, help!.ActiveParameter);
    }

    [Fact]
    public async Task References_finds_all_call_sites_across_scripts()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        fixture.WriteScript("Other", "ScriptName Other Extends Foo\nFunction Baz()\n  Bar()\nEndFunction\n");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var references = await fixture.Scheduler.ExecuteAsync(
            new ReferencesHandler(),
            new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
                Context = new ReferenceContext { IncludeDeclaration = true },
            },
            timeout.Token
        );

        Assert.Equal(3, references.Length);
        Assert.Contains(references, r => r.Uri == fixture.ScriptUri && r.Range.Start!.Line == 1);
        Assert.Contains(references, r => r.Uri == fixture.ScriptUri && r.Range.Start!.Line == 2);
        Assert.Contains(references, r => r.Uri != fixture.ScriptUri);
    }

    [Fact]
    public async Task References_excludes_declaration_when_not_requested()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var references = await fixture.Scheduler.ExecuteAsync(
            new ReferencesHandler(),
            new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
                Context = new ReferenceContext { IncludeDeclaration = false },
            },
            timeout.Token
        );

        var location = Assert.Single(references);
        Assert.Equal(2, location.Range.Start!.Line);
    }

    [Fact]
    public async Task Rename_edits_every_reference_across_scripts()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        var otherPath = fixture.WriteScript(
            "Other",
            "ScriptName Other Extends Foo\nFunction Baz()\n  Bar()\nEndFunction\n"
        );
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var edit = await fixture.Scheduler.ExecuteAsync(
            new RenameHandler(),
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
                NewName = "Greet",
            },
            timeout.Token
        );

        Assert.NotNull(edit?.Changes);
        Assert.Equal(2, edit.Changes.Count);
        var fooEdits = edit.Changes[fixture.ScriptUri];
        Assert.Equal(2, fooEdits.Length);
        Assert.All(fooEdits, e => Assert.Equal("Greet", e.NewText));
        var otherUri = LspTestWorkspace.ToFileUri(otherPath);
        var otherEdits = edit.Changes[otherUri];
        Assert.Equal("Greet", Assert.Single(otherEdits).NewText);
    }

    [Fact]
    public async Task Rename_rejects_native_symbols()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar() Native\nFunction Baz()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var edit = await fixture.Scheduler.ExecuteAsync(
            new RenameHandler(),
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
                NewName = "Greet",
            },
            timeout.Token
        );

        Assert.Null(edit);
    }

    [Fact]
    public async Task PrepareRename_reports_identifier_span_and_placeholder()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var result = await fixture.Scheduler.ExecuteAsync(
            new PrepareRenameHandler(),
            new TextDocumentPositionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
            },
            timeout.Token
        );

        Assert.NotNull(result);
        Assert.Equal("Bar", result!.Placeholder);
        Assert.Equal(1, result.Range.Start!.Line);
    }

    [Fact]
    public async Task PrepareRename_rejects_native_symbols()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar() Native\nFunction Baz()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var result = await fixture.Scheduler.ExecuteAsync(
            new PrepareRenameHandler(),
            new TextDocumentPositionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
            },
            timeout.Token
        );

        Assert.Null(result);
    }

    private static bool Contains(Range outer, Range inner)
    {
        var startsBefore =
            outer.Start!.Line < inner.Start!.Line
            || (
                outer.Start.Line == inner.Start.Line
                && outer.Start.Character <= inner.Start.Character
            );
        var endsAfter =
            outer.End!.Line > inner.End!.Line
            || (outer.End.Line == inner.End.Line && outer.End.Character >= inner.End.Character);
        return startsBefore && endsAfter;
    }

    private static System.Collections.Generic.IEnumerable<SyntaxTreeNode> DescendantsAndSelf(
        SyntaxTreeNode node
    )
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
