using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal sealed class HoverHandler : ILspHandler<TextDocumentPositionParams?, Hover?>
{
    internal const string MethodName = "textDocument/hover";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Hover?> HandleAsync(
        TextDocumentPositionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult<Hover?>(null);
        }

        var lineMap = tree.GetText().Lines;
        var offset = lineMap.GetOffset(p.Position.Line, p.Position.Character);
        var symbol = model.GetSymbolInfo(offset, ct);
        if (symbol is null)
        {
            return Task.FromResult<Hover?>(null);
        }

        return Task.FromResult<Hover?>(
            new Hover
            {
                Contents = new MarkupContent { Value = Markdown(symbol) },
                Range = model.FindNode(offset, ct) is { } name
                    ? DocumentSymbolHandler.ToRange(lineMap, name.Span)
                    : null,
            }
        );
    }

    // The declaration as a highlighted Papyrus code block, then its doc comment.
    internal static string Markdown(ISymbol symbol)
    {
        var markdown = "```papyrus\n" + SymbolDisplay.ToDeclarationString(symbol) + "\n```";
        return SymbolDisplay.GetDocumentation(symbol) is { } documentation
            ? markdown + "\n\n" + documentation
            : markdown;
    }
}

internal sealed class DefinitionHandler : ILspHandler<DefinitionParams?, Location[]>
{
    internal const string MethodName = "textDocument/definition";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Location[]> HandleAsync(
        DefinitionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out var path,
                out var projectId
            )
        )
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var symbol = model.GetSymbolInfo(offset, ct);
        if (symbol is null)
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        // A declaration in another script is found through the script that owns it.
        var owner = symbol as IScriptSymbol ?? symbol.ContainingScript;
        var locations = new List<Location>();
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree is not { } declaringTree)
            {
                continue;
            }

            string uri;
            if (declaringTree == tree)
            {
                uri = p.TextDocument?.Uri ?? LspUris.ToUri(path);
            }
            else if (
                owner is not null
                && snapshot.Solution.TryGetScriptPath(projectId, owner.QualifiedName, out var ownerPath)
            )
            {
                uri = LspUris.ToUri(ownerPath);
            }
            else
            {
                continue;
            }

            locations.Add(
                new Location
                {
                    Uri = uri,
                    Range = DocumentSymbolHandler.ToRange(declaringTree.GetText().Lines, reference.Span),
                }
            );
        }

        return Task.FromResult(locations.ToArray());
    }
}

internal sealed class CompletionHandler : ILspHandler<CompletionParams?, CompletionItem[]>
{
    internal const string MethodName = "textDocument/completion";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CompletionItem[]> HandleAsync(
        CompletionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult(Array.Empty<CompletionItem>());
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var container = model.TryGetMemberAccessContainer(offset, out var containerType, ct)
            ? containerType
            : null;
        var symbols = model.LookupSymbols(offset, container);
        var uri = p.TextDocument?.Uri ?? "";

        var items = new CompletionItem[symbols.Length];
        for (var i = 0; i < symbols.Length; i++)
        {
            items[i] = ToCompletionItem(symbols[i], uri, offset);
        }

        return Task.FromResult(items);
    }

    private static CompletionItem ToCompletionItem(ISymbol symbol, string uri, int offset)
    {
        return new CompletionItem
        {
            Label = symbol.Name.Text,
            Kind = ToLspKind(symbol),
            Detail = SymbolDisplay.ToDeclarationString(symbol),
            CommitCharacters = symbol.Kind is SymbolKind.Function or SymbolKind.Event
                ? CommitCharactersOpenParen
                : null,
            Data = new CompletionItemData
            {
                Uri = uri,
                Offset = offset,
                Name = symbol.Name.Text,
                Kind = (int)symbol.Kind,
            },
        };
    }

    private static readonly string[] CommitCharactersOpenParen = ["("];

    private static int ToLspKind(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Function or SymbolKind.Event => 3,
            SymbolKind.Property => 10,
            SymbolKind.Variable or SymbolKind.Parameter => 6,
            SymbolKind.Script => 7,
            SymbolKind.Struct => 22,
            _ => 1,
        };
    }
}

/// <summary>
/// Handles <c>completionItem/resolve</c>. Per the LSP spec this request carries only
/// the <see cref="CompletionItem"/> returned earlier (via its <see cref="CompletionItem.Data"/>),
/// not the original document/position, so the symbol lookup is redone from scratch here.
/// For an inherited <see cref="SymbolKind.Event"/> that the script does not yet override,
/// this expands the item into a full event-skeleton snippet built from the event's own
/// signature in the symbol graph (see #190), rather than a static snippet file.
/// </summary>
internal sealed class CompletionResolveHandler : ILspHandler<CompletionItem?, CompletionItem?>
{
    internal const string MethodName = "completionItem/resolve";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CompletionItem?> HandleAsync(
        CompletionItem? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (p is null || p.Data is not { } data)
        {
            return Task.FromResult<CompletionItem?>(p);
        }

        if (
            !LspDocuments.TryGetSemanticModel(
                snapshot,
                new TextDocumentIdentifier { Uri = data.Uri },
                out var model,
                out _,
                out _
            )
        )
        {
            return Task.FromResult<CompletionItem?>(p);
        }

        var container = model.TryGetMemberAccessContainer(
            data.Offset,
            out var containerType,
            ct
        )
            ? containerType
            : null;

        var kind = (SymbolKind)data.Kind;
        ISymbol? symbol = null;
        foreach (var candidate in model.LookupSymbols(data.Offset, container))
        {
            if (candidate.Kind == kind && candidate.Name.Text == data.Name)
            {
                symbol = candidate;
                break;
            }
        }

        if (symbol is not null && SymbolDisplay.GetDocumentation(symbol) is { } documentation)
        {
            p.Documentation = new MarkupContent { Value = documentation };
        }

        if (
            symbol is IEventSymbol eventSymbol
            && !model.DeclaresMemberDirectly(eventSymbol.Name, SymbolKind.Event)
        )
        {
            p.InsertText = BuildEventSnippet(eventSymbol);
            p.InsertTextFormat = 2;
        }

        return Task.FromResult<CompletionItem?>(p);
    }

    private static string BuildEventSnippet(IEventSymbol eventSymbol)
    {
        var parameters = string.Join(
            ", ",
            eventSymbol.Parameters.Select(parameter =>
                $"{SymbolDisplay.TypeText(parameter.Type)} {parameter.Name.Text}"
            )
        );

        return $"Event {eventSymbol.Name.Text}({parameters})\n\t$0\nEndEvent";
    }
}

internal sealed class ReferencesHandler : ILspHandler<ReferenceParams?, Location[]>
{
    internal const string MethodName = "textDocument/references";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<Location[]> HandleAsync(
        ReferenceParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _,
                out var projectId
            )
        )
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        var symbol = model.GetSymbolInfo(offset, ct);
        if (symbol is null)
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        var includeDeclaration = p.Context?.IncludeDeclaration ?? false;
        var results = snapshot.Solution.FindReferences(projectId, symbol, ct);
        var locations = new List<Location>();
        foreach (var script in results)
        {
            var uri = LspUris.ToUri(script.Path);
            var lineMap = script.Tree.GetText().Lines;
            foreach (var reference in script.Spans)
            {
                if (!includeDeclaration && reference.IsDeclaration)
                {
                    continue;
                }

                locations.Add(
                    new Location
                    {
                        Uri = uri,
                        Range = DocumentSymbolHandler.ToRange(lineMap, reference.Span),
                    }
                );
            }
        }

        return Task.FromResult(locations.ToArray());
    }
}

internal sealed class PrepareRenameHandler
    : ILspHandler<TextDocumentPositionParams?, PrepareRenameResult?>
{
    internal const string MethodName = "textDocument/prepareRename";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<PrepareRenameResult?> HandleAsync(
        TextDocumentPositionParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult<PrepareRenameResult?>(null);
        }

        var lineMap = tree.GetText().Lines;
        var offset = lineMap.GetOffset(p.Position.Line, p.Position.Character);
        if (!model.TryGetRenameInfo(offset, ct, out var span, out _))
        {
            return Task.FromResult<PrepareRenameResult?>(null);
        }

        return Task.FromResult<PrepareRenameResult?>(
            new PrepareRenameResult
            {
                Range = DocumentSymbolHandler.ToRange(lineMap, span),
                Placeholder = tree.GetText().ToString(span),
            }
        );
    }
}

internal sealed class RenameHandler : ILspHandler<RenameParams?, WorkspaceEdit?>
{
    internal const string MethodName = "textDocument/rename";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<WorkspaceEdit?> HandleAsync(
        RenameParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            string.IsNullOrEmpty(p?.NewName)
            || p.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _,
                out var projectId
            )
        )
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        if (!model.TryGetRenameInfo(offset, ct, out _, out var symbol))
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        var results = snapshot.Solution.FindReferences(projectId, symbol, ct);
        var changes = new Dictionary<string, TextEdit[]>();
        foreach (var script in results)
        {
            var uri = LspUris.ToUri(script.Path);
            var lineMap = script.Tree.GetText().Lines;
            var edits = new TextEdit[script.Spans.Length];
            for (var i = 0; i < script.Spans.Length; i++)
            {
                edits[i] = new TextEdit
                {
                    Range = DocumentSymbolHandler.ToRange(lineMap, script.Spans[i].Span),
                    NewText = p.NewName,
                };
            }

            changes[uri] = edits;
        }

        return Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit { Changes = changes });
    }
}

internal sealed class DocumentHighlightHandler
    : ILspHandler<DocumentHighlightParams?, DocumentHighlight[]>
{
    internal const string MethodName = "textDocument/documentHighlight";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<DocumentHighlight[]> HandleAsync(
        DocumentHighlightParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult(Array.Empty<DocumentHighlight>());
        }

        var lineMap = tree.GetText().Lines;
        var offset = lineMap.GetOffset(p.Position.Line, p.Position.Character);
        var occurrences = model.GetDocumentHighlights(offset, ct);

        var highlights = new DocumentHighlight[occurrences.Length];
        for (var i = 0; i < occurrences.Length; i++)
        {
            highlights[i] = new DocumentHighlight
            {
                Range = DocumentSymbolHandler.ToRange(lineMap, occurrences[i].Span),
                Kind = occurrences[i].IsDeclaration ? 3 : 2,
            };
        }

        return Task.FromResult(highlights);
    }
}

internal sealed class InlayHintHandler : ILspHandler<InlayHintParams?, InlayHint[]>
{
    internal const string MethodName = "textDocument/inlayHint";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<InlayHint[]> HandleAsync(
        InlayHintParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            !LspDocuments.TryGetSemanticModel(
                snapshot,
                p?.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult(Array.Empty<InlayHint>());
        }

        var lineMap = tree.GetText().Lines;
        var textLength = tree.GetText().Length;
        var span =
            p!.Range is { Start: { } start, End: { } end }
                ? new Papyrus.CodeAnalysis.Text.TextSpan(
                    lineMap.GetOffset(start.Line, start.Character),
                    lineMap.GetOffset(end.Line, end.Character)
                        - lineMap.GetOffset(start.Line, start.Character)
                )
                : new Papyrus.CodeAnalysis.Text.TextSpan(0, textLength);

        var hints = model.GetInlayHints(span, ct);

        var items = new InlayHint[hints.Length];
        for (var i = 0; i < hints.Length; i++)
        {
            var (line, character) = lineMap.GetPosition(hints[i].Position);
            items[i] = new InlayHint
            {
                Position = new Position { Line = line, Character = character },
                Label = $"{hints[i].ParameterName.Text}:",
                Kind = 2,
                PaddingRight = true,
            };
        }

        return Task.FromResult(items);
    }
}

internal sealed class SignatureHelpHandler : ILspHandler<SignatureHelpParams?, SignatureHelp?>
{
    internal const string MethodName = "textDocument/signatureHelp";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<SignatureHelp?> HandleAsync(
        SignatureHelpParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            p?.Position is null
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                p.TextDocument,
                out var model,
                out var tree,
                out _
            )
        )
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        var offset = tree.GetText().Lines.GetOffset(p.Position.Line, p.Position.Character);
        if (!model.TryGetInvocationContext(offset, out var function, out var activeParameter, ct))
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        var parameters = new ParameterInformation[function!.Parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = function.Parameters[i];
            parameters[i] = new ParameterInformation
            {
                Label = SymbolDisplay.ToParameterString(parameter),
            };
        }

        return Task.FromResult<SignatureHelp?>(
            new SignatureHelp
            {
                Signatures =
                [
                    new SignatureInformation
                    {
                        Label = SymbolDisplay.ToDeclarationString(function),
                        Documentation = SymbolDisplay.GetDocumentation(function) is { } documentation
                            ? new MarkupContent { Value = documentation }
                            : null,
                        Parameters = parameters,
                    },
                ],
                ActiveSignature = 0,
                ActiveParameter = Math.Min(activeParameter, Math.Max(parameters.Length - 1, 0)),
            }
        );
    }
}
