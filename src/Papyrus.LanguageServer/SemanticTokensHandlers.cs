using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Classification;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.LanguageServer;

internal static class LspSemanticTokens
{
    // Index i is the LSP token type for ClassificationKind value i; the
    // alignment is asserted by SemanticTokensHandlerTests.
    internal static readonly string[] TokenTypes =
    [
        "keyword",
        "comment",
        "string",
        "number",
        "operator",
        "type",
        "struct",
        "function",
        "event",
        "property",
        "variable",
        "parameter",
        "enumMember",
        "namespace",
    ];

    internal static readonly string[] TokenModifiers = ["declaration"];

    internal static SemanticTokensOptions CapabilityOptions() =>
        new()
        {
            Legend = new SemanticTokensLegend
            {
                TokenTypes = TokenTypes,
                TokenModifiers = TokenModifiers,
            },
            Full = true,
            Range = true,
        };

    internal static int MapKind(ClassificationKind kind) => (int)kind;

    internal static SemanticTokens Encode(SourceText text, ImmutableArray<ClassifiedSpan> spans)
    {
        var lineMap = text.Lines;
        var data = new List<int>(spans.Length * 5);
        var previousLine = 0;
        var previousCharacter = 0;
        foreach (var classified in spans)
        {
            var typeIndex = MapKind(classified.Kind);
            var modifiers = classified.IsDeclaration ? 1 : 0;

            // Clients are not required to support multi-line tokens, so a span
            // that crosses a line break (block comments) is emitted per line.
            var (line, character) = lineMap.GetPosition(classified.Span.Start);
            var end = Math.Min(classified.Span.End, lineMap.TextLength);
            var start = classified.Span.Start;
            while (start < end)
            {
                var segmentEnd = Math.Min(end, lineMap.GetLineEnd(line));
                var length = segmentEnd - start;
                if (length > 0)
                {
                    data.Add(line - previousLine);
                    data.Add(line == previousLine ? character - previousCharacter : character);
                    data.Add(length);
                    data.Add(typeIndex);
                    data.Add(modifiers);
                    previousLine = line;
                    previousCharacter = character;
                }

                if (segmentEnd >= end)
                {
                    break;
                }

                line++;
                character = 0;
                start = lineMap.GetLineStart(line);
            }
        }

        return new SemanticTokens { Data = [.. data] };
    }

    internal static SemanticTokens Handle(
        LspSnapshot snapshot,
        TextDocumentIdentifier? document,
        Range? range,
        CancellationToken ct
    )
    {
        if (!LspDocuments.TryGetTree(snapshot, document, out var tree))
        {
            return new SemanticTokens();
        }

        var text = tree.GetText();
        TextSpan? filter = null;
        if (range is { Start: { } start, End: { } end })
        {
            var lineMap = text.Lines;
            var startOffset = lineMap.GetOffset(start.Line, start.Character);
            var endOffset = Math.Max(startOffset, lineMap.GetOffset(end.Line, end.Character));
            filter = new TextSpan(startOffset, endOffset - startOffset);
        }

        var spans = SyntacticClassifier.Classify(tree, filter, ct);
        return Encode(text, spans);
    }
}

internal sealed class SemanticTokensFullHandler : ILspHandler<SemanticTokensParams?, SemanticTokens>
{
    internal const string MethodName = "textDocument/semanticTokens/full";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SemanticTokens> HandleAsync(
        SemanticTokensParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            LspSemanticTokens.Handle(snapshot, p?.TextDocument, range: null, ct)
        );
    }
}

internal sealed class SemanticTokensRangeHandler
    : ILspHandler<SemanticTokensRangeParams?, SemanticTokens>
{
    internal const string MethodName = "textDocument/semanticTokens/range";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SemanticTokens> HandleAsync(
        SemanticTokensRangeParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(LspSemanticTokens.Handle(snapshot, p?.TextDocument, p?.Range, ct));
    }
}
