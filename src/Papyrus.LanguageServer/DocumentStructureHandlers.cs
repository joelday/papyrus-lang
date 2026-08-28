using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.LanguageServer;

internal sealed class DocumentSymbolHandler : ILspHandler<DocumentSymbolParams?, DocumentSymbol[]>
{
    internal const string MethodName = "textDocument/documentSymbol";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<DocumentSymbol[]> HandleAsync(
        DocumentSymbolParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (!LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult(Array.Empty<DocumentSymbol>());
        }

        var lineMap = tree.GetText().Lines;
        var symbols = SyntaxStructure.GetDocumentSymbols(tree, ct);
        var result = new DocumentSymbol[symbols.Length];
        for (var i = 0; i < symbols.Length; i++)
        {
            result[i] = CreateSymbol(symbols[i], lineMap, ct);
        }

        return Task.FromResult(result);
    }

    private static DocumentSymbol CreateSymbol(
        Papyrus.CodeAnalysis.Structure.DocumentSymbol symbol,
        TextLineMap lineMap,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var children = new DocumentSymbol[symbol.Children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = CreateSymbol(symbol.Children[i], lineMap, ct);
        }

        return new DocumentSymbol
        {
            Name = symbol.Name,
            Kind = ToLspKind(symbol.Kind),
            Range = ToRange(lineMap, symbol.Span),
            SelectionRange = ToRange(lineMap, symbol.SelectionSpan),
            Children = children,
        };
    }

    private static int ToLspKind(DocumentSymbolKind kind)
    {
        return kind switch
        {
            DocumentSymbolKind.Class => 5,
            DocumentSymbolKind.Method => 6,
            DocumentSymbolKind.Property => 7,
            DocumentSymbolKind.Field => 8,
            DocumentSymbolKind.Namespace => 3,
            DocumentSymbolKind.Struct => 23,
            DocumentSymbolKind.Event => 24,
            _ => 19,
        };
    }

    internal static Range ToRange(TextLineMap lineMap, TextSpan span)
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

internal sealed class DocumentSyntaxTreeHandler
    : ILspHandler<DocumentSyntaxTreeParams?, DocumentSyntaxTree?>
{
    internal const string MethodName = "textDocument/syntaxTree";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<DocumentSyntaxTree?> HandleAsync(
        DocumentSyntaxTreeParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (!LspDocuments.TryGetTree(snapshot, p?.TextDocument, out var tree))
        {
            return Task.FromResult<DocumentSyntaxTree?>(null);
        }

        var lineMap = tree.GetText().Lines;
        return Task.FromResult<DocumentSyntaxTree?>(
            new DocumentSyntaxTree
            {
                Root = CreateNode(SyntaxStructure.GetSyntaxTree(tree), lineMap, ct),
            }
        );
    }

    private static SyntaxTreeNode CreateNode(
        Papyrus.CodeAnalysis.Structure.SyntaxTreeNode node,
        TextLineMap lineMap,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var children = new SyntaxTreeNode[node.Children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = CreateNode(node.Children[i], lineMap, ct);
        }

        return new SyntaxTreeNode
        {
            Kind = node.Kind.ToString(),
            Text = node.Text,
            Range = DocumentSymbolHandler.ToRange(lineMap, node.Span),
            IsToken = node.IsToken,
            Children = children,
        };
    }
}
