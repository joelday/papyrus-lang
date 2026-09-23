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
    public async Task Completion_offers_what_fits_the_context()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-completion-context-");
        fixture.WriteScript("Util", "ScriptName Util\nFunction Wait(Float a) Global Native\n");
        fixture.WriteScript("Other", "ScriptName Other\n");
        await fixture.OpenAsync(
            "ScriptName Foo\nimport Util\nFunction Go(Int count)\n  \n  Other o = new \nEndFunction\n\n",
            timeout.Token);

        async Task<string[]> LabelsAt(int line, int character)
        {
            var items = await fixture.Scheduler.ExecuteAsync(
                new CompletionHandler(),
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                    Position = new Position { Line = line, Character = character },
                },
                timeout.Token);
            return items.Select(static item => item.Label).ToArray();
        }

        var statement = await LabelsAt(3, 2);
        Assert.Contains("count", statement);
        Assert.Contains("Wait", statement);       // imported global
        Assert.Contains("Other", statement);      // script type
        Assert.Contains("If", statement);         // statement keyword
        Assert.DoesNotContain("EndFunction", statement);

        var type = await LabelsAt(4, 16);
        Assert.Contains("Other", type);
        Assert.Contains("Int", type);
        Assert.DoesNotContain("count", type);
        Assert.DoesNotContain("If", type);

        var declaration = await LabelsAt(6, 0);
        Assert.Contains("Function", declaration);
        Assert.Contains("Other", declaration);
        Assert.DoesNotContain("count", declaration);
    }

    [Fact]
    public async Task Hover_and_definition_resolve_other_scripts_and_ignore_keywords()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-xref-");
        var otherPath = fixture.WriteScript("Other", "ScriptName Other\nFunction Ping()\n{Pings}\nEndFunction\n");
        await fixture.OpenAsync(
            "ScriptName Foo\nOther Property P Auto\nFunction F()\n  P.Ping()\nEndFunction\n",
            timeout.Token);

        async Task<Hover?> HoverAt(int line, int character) =>
            await fixture.Scheduler.ExecuteAsync(
                new HoverHandler(),
                new TextDocumentPositionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                    Position = new Position { Line = line, Character = character },
                },
                timeout.Token);

        // A type position names the other script.
        var typeHover = await HoverAt(1, 2);
        Assert.Contains("ScriptName Other", typeHover!.Contents.Value, StringComparison.Ordinal);

        // A member of the other script shows its declaration and doc comment...
        var callHover = await HoverAt(3, 5);
        Assert.Equal("```papyrus\nFunction Ping()\n```\n\nPings", callHover!.Contents.Value);

        // ...and its definition is in the other file.
        var definition = await fixture.Scheduler.ExecuteAsync(
            new DefinitionHandler(),
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 3, Character = 5 },
            },
            timeout.Token);
        var location = Assert.Single(definition);
        Assert.Equal(new Uri(otherPath).AbsoluteUri, location.Uri);
        Assert.Equal(1, location.Range.Start!.Line);

        // A declaration keyword is not a reference to the declaration.
        Assert.Null(await HoverAt(1, 8));
    }

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

        Assert.NotNull(hover);
        Assert.Equal("markdown", hover.Contents.Kind);
        Assert.StartsWith("```papyrus\n", hover.Contents.Value, StringComparison.Ordinal);
        Assert.Contains("Function Bar", hover.Contents.Value, StringComparison.Ordinal);
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
    public async Task Completion_sets_commit_characters_and_data_for_functions_and_events()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        fixture.WriteScript("Base", "ScriptName Base\nEvent OnInit()\nEndEvent\n");
        await fixture.OpenAsync(
            "ScriptName Foo Extends Base\n" + "Function Bar()\n" + "  \n" + "EndFunction\n",
            timeout.Token
        );

        var completions = await fixture.Scheduler.ExecuteAsync(
            new CompletionHandler(),
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 2, Character = 0 },
            },
            timeout.Token
        );

        var bar = Assert.Single(completions, item => item.Label == "Bar");
        Assert.Equal(["("], bar.CommitCharacters ?? []);
        Assert.NotNull(bar.Data);
        Assert.Equal("Bar", bar.Data!.Name);

        var onInit = Assert.Single(completions, item => item.Label == "OnInit");
        Assert.Equal(["("], onInit.CommitCharacters ?? []);
    }

    [Fact]
    public async Task CompletionResolve_expands_unoverridden_event_into_skeleton_snippet()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        fixture.WriteScript(
            "Base",
            "ScriptName Base\nEvent OnInit()\nEndEvent\nEvent OnLoad(Int a)\nEndEvent\n"
        );
        await fixture.OpenAsync(
            "ScriptName Foo Extends Base\n"
                + "Event OnInit()\n"
                + "EndEvent\n"
                + "Function Bar()\n"
                + "  \n"
                + "EndFunction\n",
            timeout.Token
        );

        var completions = await fixture.Scheduler.ExecuteAsync(
            new CompletionHandler(),
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 4, Character = 0 },
            },
            timeout.Token
        );

        var onLoad = Assert.Single(completions, item => item.Label == "OnLoad");
        var resolvedOnLoad = await fixture.Scheduler.ExecuteAsync(
            new CompletionResolveHandler(),
            onLoad,
            timeout.Token
        );
        Assert.Equal(2, resolvedOnLoad?.InsertTextFormat);
        Assert.Contains("Event OnLoad(Int a)", resolvedOnLoad?.InsertText);
        Assert.Contains("EndEvent", resolvedOnLoad?.InsertText);

        var onInit = completions.First(item => item.Label == "OnInit");
        var resolvedOnInit = await fixture.Scheduler.ExecuteAsync(
            new CompletionResolveHandler(),
            onInit,
            timeout.Token
        );
        Assert.Null(resolvedOnInit?.InsertText);
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
        Assert.Equal("Function Bar(Int a, Int b)", signature.Label);
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

    [Fact]
    public async Task DocumentHighlight_finds_declaration_and_call_sites_in_current_file()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        fixture.WriteScript(
            "Other",
            "ScriptName Other Extends Foo\nFunction Baz()\n  Bar()\nEndFunction\n"
        );
        await fixture.OpenAsync(
            "ScriptName Foo\nFunction Bar()\n  Bar()\nEndFunction\n",
            timeout.Token
        );

        var highlights = await fixture.Scheduler.ExecuteAsync(
            new DocumentHighlightHandler(),
            new DocumentHighlightParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Position = new Position { Line = 1, Character = 10 },
            },
            timeout.Token
        );

        // Only the current file's own declaration + call site, not the Other.psc call.
        Assert.Equal(2, highlights.Length);
        Assert.Contains(highlights, h => h.Range.Start!.Line == 1 && h.Kind == 3);
        Assert.Contains(highlights, h => h.Range.Start!.Line == 2 && h.Kind == 2);
    }

    [Fact]
    public async Task InlayHint_reports_parameter_names_for_positional_call_arguments()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync(
            "ScriptName Foo\n"
                + "Function Bar(Int a, Int b)\n"
                + "EndFunction\n"
                + "Function Baz()\n"
                + "  Bar(1, 2)\n"
                + "EndFunction\n",
            timeout.Token
        );

        var hints = await fixture.Scheduler.ExecuteAsync(
            new InlayHintHandler(),
            new InlayHintParams { TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri } },
            timeout.Token
        );

        Assert.Equal(2, hints.Length);
        Assert.Contains(hints, h => h.Label == "a:");
        Assert.Contains(hints, h => h.Label == "b:");
        Assert.All(hints, h => Assert.Equal(4, h.Position.Line));
    }

    [Fact]
    public async Task Diagnostic_pull_reports_binding_errors_for_the_open_document()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync("ScriptName Foo\nUnknownType Baz\n", timeout.Token);

        var report = await fixture.Scheduler.ExecuteAsync(
            new DiagnosticHandler(),
            new DiagnosticParams { TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri } },
            timeout.Token
        );

        Assert.Equal("full", report.Kind);
        Assert.NotEmpty(report.Items);
        Assert.All(report.Items, d => Assert.Equal("papyrus", d.Source));
        Assert.All(report.Items, d => Assert.NotNull(d.Range));
    }

    [Fact]
    public async Task PublishDiagnostics_is_pushed_when_the_workspace_changes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        using var rpc = new LanguageServerRpc(fixture.Scheduler);
        var notifier = new RecordingNotifier();
        rpc.Notifier = notifier;

        await fixture.OpenAsync("ScriptName Foo\nUnknownType Baz\n", timeout.Token);

        // The push runs fire-and-forget off the workspace-changed event; give it a
        // moment to land rather than coupling the test to that implementation detail.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (notifier.Publishes.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.True(notifier.Publishes.TryDequeue(out var published));
        Assert.Equal(fixture.ScriptUri, published.Uri);
        Assert.NotEmpty(published.Diagnostics ?? []);
    }

    private sealed class RecordingNotifier : ILspNotifier
    {
        internal readonly System.Collections.Concurrent.ConcurrentQueue<PublishDiagnosticsParams> Publishes = new();

        public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken)
        {
            if (method == "textDocument/publishDiagnostics" && parameters is PublishDiagnosticsParams p)
            {
                Publishes.Enqueue(p);
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ScriptInfo_resolves_identifier_and_search_paths_for_the_open_document()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync("ScriptName Foo\n", timeout.Token);

        var info = await fixture.Scheduler.ExecuteAsync(
            new DocumentScriptInfoHandler(),
            new DocumentScriptInfoParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token
        );

        Assert.Contains("Foo", info.Identifiers);
        Assert.NotEmpty(info.SearchPaths);
        var identifierFiles = Assert.Single(info.IdentifierFiles);
        Assert.Equal("Foo", identifierFiles.Identifier);
        Assert.Equal(fixture.ScriptPath, Assert.Single(identifierFiles.Files));
    }

    [Fact]
    public async Task ProjectInfos_reports_the_loaded_project_and_its_scripts()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync("ScriptName Foo\n", timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new ProjectInfosHandler(),
            new ProjectInfosParams(),
            timeout.Token
        );

        var project = Assert.Single(result.Projects);
        var include = Assert.Single(project.SourceIncludes);
        Assert.True(include.IsImport);
        Assert.Contains(include.Scripts, s => s.Identifier == "Foo");
    }

    [Fact]
    public async Task Assembly_disassembles_the_open_document()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-structure-");
        await fixture.OpenAsync("ScriptName Foo\n", timeout.Token);

        var result = await fixture.Scheduler.ExecuteAsync(
            new DocumentAssemblyHandler(),
            new DocumentAssemblyParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token
        );

        Assert.NotNull(result.Assembly);
        Assert.Contains(".object Foo", result.Assembly);
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
