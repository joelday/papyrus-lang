using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.LanguageServer;

internal sealed class FoldingRangeHandler : ILspHandler<FoldingRangeParams?, FoldingRange[]>
{
    internal const string MethodName = "textDocument/foldingRange";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<FoldingRange[]> HandleAsync(
        FoldingRangeParams? p,
        LspSnapshot snapshot,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(Array.Empty<FoldingRange>());
        }

        var lineMap = tree.GetText().Lines;
        var spans = SyntaxStructure.GetFoldingSpans(tree, ct);
        var result = new List<FoldingRange>(spans.Length);
        foreach (var folding in spans)
        {
            var (startLine, _) = lineMap.GetPosition(folding.Span.Start);
            var (endLine, _) = lineMap.GetPosition(folding.Span.End - 1);
            if (endLine > startLine)
            {
                result.Add(new FoldingRange
                {
                    StartLine = startLine,
                    EndLine = endLine,
                    Kind = folding.Kind switch
                    {
                        FoldingSpanKind.Comment => "comment",
                        FoldingSpanKind.Imports => "imports",
                        _ => null,
                    },
                });
            }
        }

        return Task.FromResult(result.ToArray());
    }
}

internal sealed class SelectionRangeHandler : ILspHandler<SelectionRangeParams?, SelectionRange[]>
{
    internal const string MethodName = "textDocument/selectionRange";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SelectionRange[]> HandleAsync(
        SelectionRangeParams? p,
        LspSnapshot snapshot,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var positions = p?.Positions;
        if (positions is null
            || positions.Length == 0
            || !LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(Array.Empty<SelectionRange>());
        }

        var lineMap = tree.GetText().Lines;
        var result = new SelectionRange[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i] ?? new Position();
            var offset = lineMap.GetOffset(position.Line, position.Character);
            var chain = SyntaxStructure.GetSelectionSpans(tree, offset, ct);

            // Link outermost-to-innermost so each range's parent widens it.
            SelectionRange? current = null;
            for (var j = chain.Length - 1; j >= 0; j--)
            {
                current = new SelectionRange
                {
                    Range = ToRange(lineMap, chain[j]),
                    Parent = current,
                };
            }

            result[i] = current ?? new SelectionRange
            {
                Range = new Range
                {
                    Start = new Position { Line = position.Line, Character = position.Character },
                    End = new Position { Line = position.Line, Character = position.Character },
                },
            };
        }

        return Task.FromResult(result);
    }

    private static Range ToRange(TextLineMap lineMap, TextSpan span)
    {
        var (startLine, startCharacter) = lineMap.GetPosition(span.Start);
        var (endLine, endCharacter) = lineMap.GetPosition(span.End);
        return new Range
        {
            Start = new Position { Line = startLine, Character = startCharacter },
            End = new Position { Line = endLine, Character = endCharacter },
        };
    }
}
