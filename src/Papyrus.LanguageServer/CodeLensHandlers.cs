using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;
using Papyrus.CodeAnalysis.Symbols;

namespace Papyrus.LanguageServer;

/// <summary>
/// <c>textDocument/codeLens</c>: a reference-count lens over each function, property,
/// script variable, struct, and custom event. Counting is deferred to
/// <c>codeLens/resolve</c>, so only lenses the editor shows cost a reference search.
/// Engine events are left out: the engine calls them, so a count means nothing.
/// </summary>
internal sealed class CodeLensHandler : ILspHandler<CodeLensParams?, CodeLens[]>
{
    internal const string MethodName = "textDocument/codeLens";
    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CodeLens[]> HandleAsync(CodeLensParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (p?.TextDocument?.Uri is not { } uri || !LspDocuments.TryGetTree(snapshot, p.TextDocument, out var tree))
        {
            return Task.FromResult(Array.Empty<CodeLens>());
        }

        var lineMap = tree.GetText().Lines;
        var lenses = new List<CodeLens>();
        foreach (var declaration in ScriptIndex.Get(tree).Declarations)
        {
            if (declaration.Kind is IndexedDeclarationKind.Function
                or IndexedDeclarationKind.Property
                or IndexedDeclarationKind.Variable
                or IndexedDeclarationKind.Struct
                or IndexedDeclarationKind.CustomEvent)
            {
                lenses.Add(
                    new CodeLens
                    {
                        Range = DocumentSymbolHandler.ToRange(lineMap, declaration.NameSpan),
                        Data = new HierarchyItemData { Uri = uri, Offset = declaration.NameSpan.Start },
                    }
                );
            }
        }

        return Task.FromResult(lenses.ToArray());
    }
}

internal sealed class CodeLensResolveHandler : ILspHandler<CodeLens?, CodeLens?>
{
    internal const string MethodName = "codeLens/resolve";

    /// <summary>Client command that shows the references (the extension converts the arguments).</summary>
    internal const string ShowReferencesCommand = "papyrus.showReferences";

    public string Method => MethodName;
    public bool MutatesSolution => false;

    public Task<CodeLens?> HandleAsync(CodeLens? lens, LspSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (lens?.Data is not { } data
            || !LspDocuments.TryGetSemanticModel(
                snapshot,
                new TextDocumentIdentifier { Uri = data.Uri },
                out var model,
                out _,
                out _,
                out var projectId)
            || model.GetSymbolInfo(data.Offset, ct) is not { } symbol)
        {
            return Task.FromResult(lens);
        }

        var locations = new List<Location>();
        foreach (var script in snapshot.Solution.FindReferences(projectId, symbol, ct))
        {
            var lineMap = script.Tree.GetText().Lines;
            foreach (var reference in script.Spans)
            {
                if (!reference.IsDeclaration)
                {
                    locations.Add(
                        new Location { Uri = LspUris.ToUri(script.Path), Range = DocumentSymbolHandler.ToRange(lineMap, reference.Span) }
                    );
                }
            }
        }

        lens.Command = new Command
        {
            Title = locations.Count == 1 ? "1 reference" : $"{locations.Count} references",
            // A lens with no references has nothing to show.
            CommandName = locations.Count == 0 ? "" : ShowReferencesCommand,
            Arguments = locations.Count == 0
                ? null
                : [new ShowReferencesArguments { Uri = data.Uri, Position = lens.Range.Start, Locations = locations.ToArray() }],
        };
        return Task.FromResult<CodeLens?>(lens);
    }
}
