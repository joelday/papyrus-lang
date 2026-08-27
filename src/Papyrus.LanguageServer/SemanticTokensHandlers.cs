using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Classification;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal static class LspSemanticTokens
{
    // Indexes into TokenTypes; must stay aligned with MapKind.
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

    internal static SemanticTokensOptions CapabilityOptions() => new()
    {
        Legend = new SemanticTokensLegend
        {
            TokenTypes = TokenTypes,
            TokenModifiers = TokenModifiers,
        },
        Full = true,
        Range = true,
    };

    internal static int MapKind(ClassificationKind kind) => kind switch
    {
        ClassificationKind.Keyword => 0,
        ClassificationKind.Comment => 1,
        ClassificationKind.String => 2,
        ClassificationKind.Number => 3,
        ClassificationKind.Operator => 4,
        ClassificationKind.TypeName => 5,
        ClassificationKind.StructName => 6,
        ClassificationKind.FunctionName => 7,
        ClassificationKind.EventName => 8,
        ClassificationKind.PropertyName => 9,
        ClassificationKind.VariableName => 10,
        ClassificationKind.ParameterName => 11,
        ClassificationKind.StateName => 12,
        ClassificationKind.GroupName => 13,
        _ => 10,
    };

    internal static SemanticTokens Encode(SourceText text, ImmutableArray<ClassifiedSpan> spans)
    {
        var lineMap = LspLineMap.Create(text);
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
            var end = classified.Span.End;
            var start = classified.Span.Start;
            while (start < end)
            {
                var lineEnd = lineMap.GetLineEnd(line);
                var segmentEnd = Math.Min(end, lineEnd);
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
}

/// <summary>Maps between character offsets and LSP line/character positions.</summary>
internal sealed class LspLineMap
{
    private readonly int[] _lineStarts;
    private readonly int[] _lineContentEnds;
    private readonly int _length;

    private LspLineMap(int[] lineStarts, int[] lineContentEnds, int length)
    {
        _lineStarts = lineStarts;
        _lineContentEnds = lineContentEnds;
        _length = length;
    }

    internal static LspLineMap Create(SourceText text)
    {
        var starts = new List<int> { 0 };
        var contentEnds = new List<int>();
        var length = text.Length;
        for (var i = 0; i < length; i++)
        {
            var c = text.GetChar(i);
            if (c is '\r' or '\n')
            {
                contentEnds.Add(i);
                if (c == '\r' && i + 1 < length && text.GetChar(i + 1) == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        contentEnds.Add(length);
        return new LspLineMap([.. starts], [.. contentEnds], length);
    }

    internal int LineCount => _lineStarts.Length;

    internal (int Line, int Character) GetPosition(int position)
    {
        position = Math.Clamp(position, 0, _length);
        var index = Array.BinarySearch(_lineStarts, position);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return (index, position - _lineStarts[index]);
    }

    internal int GetLineStart(int line)
    {
        return line < _lineStarts.Length ? _lineStarts[line] : _length;
    }

    /// <summary>End of the line's content, excluding the line break.</summary>
    internal int GetLineEnd(int line)
    {
        return line < _lineContentEnds.Length ? _lineContentEnds[line] : _length;
    }

    internal int GetOffset(int line, int character)
    {
        if (line < 0)
        {
            return 0;
        }

        if (line >= _lineStarts.Length)
        {
            return _length;
        }

        var start = _lineStarts[line];
        var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] : _length;
        return Math.Min(start + Math.Max(character, 0), end);
    }
}

internal static class SemanticTokensDocuments
{
    internal static bool TryGetTree(
        LspSnapshot snapshot,
        TextDocumentIdentifier? document,
        out SyntaxTree tree)
    {
        tree = null!;
        if (!LspUris.TryGetFilePath(document?.Uri, out var path))
        {
            return false;
        }

        var ids = snapshot.Solution.GetDocumentIdsForPath(path);
        return !ids.IsDefaultOrEmpty && snapshot.Solution.TryGetSyntaxTree(ids[0], out tree);
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
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!SemanticTokensDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(new SemanticTokens());
        }

        var spans = SyntacticClassifier.Classify(tree, span: null, ct);
        return Task.FromResult(LspSemanticTokens.Encode(tree.GetText(), spans));
    }
}

internal sealed class SemanticTokensRangeHandler : ILspHandler<SemanticTokensRangeParams?, SemanticTokens>
{
    internal const string MethodName = "textDocument/semanticTokens/range";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SemanticTokens> HandleAsync(
        SemanticTokensRangeParams? p,
        LspSnapshot snapshot,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!SemanticTokensDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(new SemanticTokens());
        }

        var text = tree.GetText();
        TextSpan? filter = null;
        if (p?.Range is { Start: { } start, End: { } end })
        {
            var lineMap = LspLineMap.Create(text);
            var startOffset = lineMap.GetOffset(start.Line, start.Character);
            var endOffset = Math.Max(startOffset, lineMap.GetOffset(end.Line, end.Character));
            filter = new TextSpan(startOffset, endOffset - startOffset);
        }

        var spans = SyntacticClassifier.Classify(tree, filter, ct);
        return Task.FromResult(LspSemanticTokens.Encode(text, spans));
    }
}
