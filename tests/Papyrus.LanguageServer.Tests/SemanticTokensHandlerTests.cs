using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Classification;
using Papyrus.LanguageServer;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class SemanticTokensHandlerTests
{
    [Fact]
    public void Token_modifier_legend_matches_classification_modifier_bits()
    {
        var modifiers = System.Enum.GetValues<Papyrus.CodeAnalysis.Classification.ClassificationModifiers>()
            .Where(static m => m != Papyrus.CodeAnalysis.Classification.ClassificationModifiers.None)
            .ToArray();

        Assert.Equal(modifiers.Length, LspSemanticTokens.TokenModifiers.Length);
        for (var i = 0; i < modifiers.Length; i++)
        {
            Assert.Equal(1 << i, (int)modifiers[i]);
            Assert.Equal(modifiers[i].ToString(), LspSemanticTokens.TokenModifiers[i], ignoreCase: true);
        }
    }

    [Fact]
    public async Task Full_returns_delta_encoded_tokens_for_open_document()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-tokens-");
        await fixture.OpenAsync("ScriptName Foo\nInt myVar = 1\n", timeout.Token);
        var handler = new SemanticTokensFullHandler();

        var result = await fixture.Scheduler.ExecuteAsync(
            handler,
            new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            },
            timeout.Token);

        var decoded = Decode(result.Data);
        Assert.Contains((0, 0, 10, "keyword", 0), decoded);          // ScriptName
        Assert.Contains((0, 11, 3, "type", 1), decoded);             // Foo (declaration)
        Assert.Contains((1, 0, 3, "keyword", 0), decoded);           // Int
        Assert.Contains((1, 4, 5, "variable", 1), decoded);          // myVar (declaration)
        Assert.Contains((1, 12, 1, "number", 0), decoded);           // 1
    }

    [Fact]
    public async Task Range_returns_only_tokens_intersecting_the_range()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-tokens-");
        await fixture.OpenAsync("ScriptName Foo\nInt a = 1\nInt b = 2\n", timeout.Token);
        var handler = new SemanticTokensRangeHandler();

        var result = await fixture.Scheduler.ExecuteAsync(
            handler,
            new SemanticTokensRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
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

    [Fact]
    public async Task Range_with_extreme_character_values_does_not_crash()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-tokens-");
        await fixture.OpenAsync("ScriptName Foo\nInt a = 1\n", timeout.Token);
        var handler = new SemanticTokensRangeHandler();

        var result = await fixture.Scheduler.ExecuteAsync(
            handler,
            new SemanticTokensRangeParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
                Range = new Range
                {
                    Start = new Position { Line = 0, Character = int.MaxValue },
                    End = new Position { Line = int.MaxValue, Character = int.MaxValue },
                },
            },
            timeout.Token);

        var decoded = Decode(result.Data);
        Assert.Contains((1, 0, 3, "keyword", 0), decoded);           // Int
    }

    [Fact]
    public async Task Unknown_document_returns_empty_tokens()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = LspTestWorkspace.Create("papyrus-lsp-tokens-");
        await fixture.OpenAsync("ScriptName Foo\n", timeout.Token);
        var handler = new SemanticTokensFullHandler();

        var result = await fixture.Scheduler.ExecuteAsync(
            handler,
            new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = LspTestWorkspace.ToFileUri(
                        Path.Combine(fixture.Root, "scripts", "Missing.psc")),
                },
            },
            timeout.Token);

        Assert.Empty(result.Data);
    }

    [Fact]
    public void Encoder_splits_multi_line_spans_per_line()
    {
        var text = Papyrus.CodeAnalysis.Text.SourceText.From(";/ a\nb /;\nInt");
        var spans = System.Collections.Immutable.ImmutableArray.Create(
            new ClassifiedSpan(
                new Papyrus.CodeAnalysis.Text.TextSpan(0, 9),
                ClassificationKind.Comment),
            new ClassifiedSpan(
                new Papyrus.CodeAnalysis.Text.TextSpan(10, 3),
                ClassificationKind.Keyword));

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

    [Fact]
    public void Encoder_clamps_spans_past_the_end_of_text()
    {
        var text = Papyrus.CodeAnalysis.Text.SourceText.From("Int");
        var spans = System.Collections.Immutable.ImmutableArray.Create(
            new ClassifiedSpan(
                new Papyrus.CodeAnalysis.Text.TextSpan(0, 100),
                ClassificationKind.Keyword));

        var decoded = Decode(LspSemanticTokens.Encode(text, spans).Data);

        Assert.Equal(
            new List<(int, int, int, string, int)> { (0, 0, 3, "keyword", 0) },
            decoded);
    }

    [Fact]
    public void Token_type_legend_aligns_with_classification_kinds()
    {
        var kinds = Enum.GetValues<ClassificationKind>();
        Assert.Equal(kinds.Length, LspSemanticTokens.TokenTypes.Length);
        foreach (var kind in kinds)
        {
            var index = LspSemanticTokens.MapKind(kind);
            Assert.InRange(index, 0, LspSemanticTokens.TokenTypes.Length - 1);
            Assert.Equal((int)kind, index);
        }
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
}
