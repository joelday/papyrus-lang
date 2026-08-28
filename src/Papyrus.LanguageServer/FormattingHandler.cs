using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;
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
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(Array.Empty<TextEdit>());
        }

        var options = p?.Options ?? new FormattingOptions();
        var edits = SyntaxFormatter.GetIndentationEdits(
            tree,
            Math.Max(1, options.TabSize),
            useTabs: !options.InsertSpaces);
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

        return Task.FromResult(result);
    }
}