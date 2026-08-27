using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.LanguageServer;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class SemanticTokensHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "papyrus-lsp-tokens-" + Guid.NewGuid().ToString("N"));

    public SemanticTokensHandlerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Full_returns_delta_encoded_tokens_for_open_document()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (scheduler, uri) = await OpenAsync(
            "ScriptName Foo\nInt myVar = 1\n",
            timeout.Token);
        using (scheduler)
        {
            var handler = new SemanticTokensFullHandler();

            var result = await scheduler.ExecuteAsync(
                handler,
                new SemanticTokensParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = uri },
                },
                timeout.Token);

            var decoded = Decode(result.Data);
            Assert.Contains((0, 0, 10, "keyword", 0), decoded);          // ScriptName
            Assert.Contains((0, 11, 3, "type", 1), decoded);             // Foo (declaration)
            Assert.Contains((1, 0, 3, "keyword", 0), decoded);           // Int
            Assert.Contains((1, 4, 5, "variable", 1), decoded);          // myVar (declaration)
            Assert.Contains((1, 12, 1, "number", 0), decoded);           // 1
        }
    }

    [Fact]
    public async Task Range_returns_only_tokens_intersecting_the_range()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (scheduler, uri) = await OpenAsync(
            "ScriptName Foo\nInt a = 1\nInt b = 2\n",
            timeout.Token);
        using (scheduler)
        {
            var handler = new SemanticTokensRangeHandler();

            var result = await scheduler.ExecuteAsync(
                handler,
                new SemanticTokensRangeParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = uri },
                    Range = new Range
                    {
                        Start = new Position { Line = 1, Character = 0 },
                        End = new Position { Line = 1, Character = 9 },
                    },
                },
                timeout.Token);

            var decoded = Decode(result.Data);
            Assert.NotEmpty(decoded);
            Assert.All(decoded, t => Assert.Equal(1, t.Line));
            Assert.Contains((1, 4, 1, "variable", 1), decoded);          // a
        }
    }

    [Fact]
    public async Task Unknown_document_returns_empty_tokens()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (scheduler, _) = await OpenAsync("ScriptName Foo\n", timeout.Token);
        using (scheduler)
        {
            var handler = new SemanticTokensFullHandler();

            var result = await scheduler.ExecuteAsync(
                handler,
                new SemanticTokensParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = ToFileUri(Path.Combine(_root, "scripts", "Missing.psc")),
                    },
                },
                timeout.Token);

            Assert.Empty(result.Data);
        }
    }

    [Fact]
    public void Encoder_splits_multi_line_spans_per_line()
    {
        var text = Papyrus.CodeAnalysis.Text.SourceText.From(";/ a\nb /;\nInt");
        var spans = System.Collections.Immutable.ImmutableArray.Create(
            new Papyrus.CodeAnalysis.Classification.ClassifiedSpan(
                new Papyrus.CodeAnalysis.Text.TextSpan(0, 9),
                Papyrus.CodeAnalysis.Classification.ClassificationKind.Comment),
            new Papyrus.CodeAnalysis.Classification.ClassifiedSpan(
                new Papyrus.CodeAnalysis.Text.TextSpan(10, 3),
                Papyrus.CodeAnalysis.Classification.ClassificationKind.Keyword));

        var decoded = Decode(LspSemanticTokens.Encode(text, spans).Data);

        Assert.Equal(
            new List<(int, int, int, string, int)>
            {
                (0, 0, 4, "comment", 0),
                (1, 0, 4, "comment", 0),
                (2, 0, 3, "keyword", 0),
            },
            decoded);
    }

    private async Task<(LspRequestScheduler Scheduler, string Uri)> OpenAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var scripts = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(scripts);
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Disk\n");
        var ppj = Path.Combine(_root, "game.ppj");
        File.WriteAllText(
            ppj,
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>scripts</Import>
              </Imports>
            </PapyrusProject>
            """);

        var workspace = new PapyrusWorkspace(TimeProvider.System, enableFileWatching: false);
        workspace.LoadProject(ppj);
        var scheduler = new LspRequestScheduler(workspace);
        var uri = ToFileUri(diskPath);
        await scheduler.ExecuteAsync(
            new DidOpenHandler(workspace),
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = "papyrus",
                    Version = 1,
                    Text = text,
                },
            },
            cancellationToken);
        return (scheduler, uri);
    }

    private static List<(int Line, int Character, int Length, string Type, int Modifiers)> Decode(int[] data)
    {
        Assert.Equal(0, data.Length % 5);
        var result = new List<(int, int, int, string, int)>();
        var line = 0;
        var character = 0;
        for (var i = 0; i < data.Length; i += 5)
        {
            line += data[i];
            character = data[i] == 0 ? character + data[i + 1] : data[i + 1];
            result.Add((line, character, data[i + 2], LspSemanticTokens.TokenTypes[data[i + 3]], data[i + 4]));
        }

        return result;
    }

    private static string ToFileUri(string path)
    {
        return new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Host = string.Empty,
            Path = Path.GetFullPath(path),
        }.Uri.AbsoluteUri;
    }
}
