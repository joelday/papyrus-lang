using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.LanguageServer;

internal sealed class FormattingHandler : ILspHandler<DocumentFormattingParams?, TextEdit[]>
{
    internal const string MethodName = "textDocument/formatting";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<TextEdit[]> HandleAsync(
        DocumentFormattingParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree)
                ? Formatting.GetEdits(tree, span: null, p?.Options)
                : Array.Empty<TextEdit>()
        );
    }
}

/// <summary><c>textDocument/rangeFormatting</c>: re-indents the lines the range touches.</summary>
internal sealed class RangeFormattingHandler : ILspHandler<DocumentRangeFormattingParams?, TextEdit[]>
{
    internal const string MethodName = "textDocument/rangeFormatting";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<TextEdit[]> HandleAsync(
        DocumentRangeFormattingParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Range is not { Start: { } start, End: { } end }
            || !LspDocuments.TryGetTree(snapshot, p.TextDocument, out var tree)
        )
        {
            return Task.FromResult(Array.Empty<TextEdit>());
        }

        var lines = tree.GetText().Lines;
        var from = lines.GetOffset(start.Line, start.Character);
        var to = lines.GetOffset(end.Line, end.Character);
        return Task.FromResult(
            Formatting.GetEdits(tree, new TextSpan(from, Math.Max(0, to - from)), p.Options)
        );
    }
}

/// <summary>
/// <c>textDocument/onTypeFormatting</c> on Enter: re-indents the line just finished when
/// it closes or continues a block (EndIf, Else, EndFunction, ...), so it snaps back to
/// the level of its opener. Other lines are left alone while the code is incomplete.
/// </summary>
internal sealed class OnTypeFormattingHandler : ILspHandler<DocumentOnTypeFormattingParams?, TextEdit[]>
{
    internal const string MethodName = "textDocument/onTypeFormatting";
    internal static readonly string[] TriggerCharacters = ["\n"];

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<TextEdit[]> HandleAsync(
        DocumentOnTypeFormattingParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is not { Line: > 0 } position
            || !LspDocuments.TryGetTree(snapshot, p.TextDocument, out var tree)
            || !SyntaxFormatter.StartsWithBlockKeyword(tree, position.Line - 1)
        )
        {
            return Task.FromResult(Array.Empty<TextEdit>());
        }

        var lines = tree.GetText().Lines;
        var lineStart = lines.GetLineStart(position.Line - 1);
        return Task.FromResult(
            Formatting.GetEdits(tree, new TextSpan(lineStart, lines.GetLineEnd(position.Line - 1) - lineStart), p.Options)
        );
    }
}

internal static class Formatting
{
    internal static TextEdit[] GetEdits(SyntaxTree tree, TextSpan? span, FormattingOptions? options)
    {
        options ??= new FormattingOptions();
        var edits = SyntaxFormatter.GetIndentationEdits(
            tree,
            span,
            Math.Max(1, options.TabSize),
            useTabs: !options.InsertSpaces
        );
        var lineMap = tree.GetText().Lines;
        var result = new TextEdit[edits.Length];
        for (var i = 0; i < edits.Length; i++)
        {
            result[i] = new TextEdit
            {
                Range = DocumentSymbolHandler.ToRange(lineMap, edits[i].Span),
                NewText = edits[i].NewText,
            };
        }

        return result;
    }
}
