using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class CodeLensHandlerTests
{
    [Fact]
    public async Task Lenses_count_references_across_scripts_when_resolved()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-lens-");
        fixture.WriteScript("Other", "ScriptName Other Extends Foo\nFunction Baz()\n  Bar()\n  Count = 2\nEndFunction\n");
        await fixture.OpenAsync(
            "ScriptName Foo\nInt Property Count Auto\nInt unused\nFunction Bar()\n  Bar()\nEndFunction\nEvent OnInit()\nEndEvent\n",
            timeout.Token
        );

        var lenses = await fixture.Scheduler.ExecuteAsync(
            new CodeLensHandler(),
            new CodeLensParams { TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri } },
            timeout.Token
        );

        // Count, unused, and Bar; not the engine event.
        Assert.Equal([1, 2, 3], lenses.Select(l => l.Range.Start!.Line).ToArray());
        var titles = new string[lenses.Length];
        for (var i = 0; i < lenses.Length; i++)
        {
            var resolved = await fixture.Scheduler.ExecuteAsync(new CodeLensResolveHandler(), lenses[i], timeout.Token);
            titles[i] = resolved!.Command!.Title;
        }

        Assert.Equal(["1 reference", "0 references", "2 references"], titles);
        var bar = lenses[2].Command!;
        Assert.Equal(CodeLensResolveHandler.ShowReferencesCommand, bar.CommandName);
        Assert.Equal(2, Assert.Single(bar.Arguments!).Locations.Length);
        Assert.Equal("", lenses[1].Command!.CommandName);

        // The command id serializes as "command", as LSP names it.
        var json = JsonSerializer.Serialize(bar, LspJsonContext.Default.Command);
        Assert.Contains("\"command\":\"papyrus.showReferences\"", json, StringComparison.Ordinal);
    }
}
