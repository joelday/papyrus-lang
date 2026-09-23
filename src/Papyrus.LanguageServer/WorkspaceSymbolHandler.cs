using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis.Structure;

namespace Papyrus.LanguageServer;

/// <summary>
/// <c>workspace/symbol</c> (Ctrl+T): declarations across every loaded project, found from
/// each script's syntax index without binding.
/// </summary>
internal sealed class WorkspaceSymbolHandler : ILspHandler<WorkspaceSymbolParams?, SymbolInformation[]>
{
    internal const string MethodName = "workspace/symbol";
    private const int Limit = 500;

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<SymbolInformation[]> HandleAsync(
        WorkspaceSymbolParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var query = p?.Query?.Trim() ?? "";
        if (query.Length == 0)
        {
            return Task.FromResult(Array.Empty<SymbolInformation>());
        }

        var results = new List<SymbolInformation>();
        foreach (var match in snapshot.Solution.FindDeclarations(query, Limit, ct))
        {
            var declaration = match.Declaration;
            results.Add(
                new SymbolInformation
                {
                    Name = declaration.Name,
                    Kind = ToLspKind(declaration.Kind),
                    ContainerName = declaration.Container,
                    Location = new Location
                    {
                        Uri = LspUris.ToUri(match.Path),
                        Range = DocumentSymbolHandler.ToRange(match.Tree.GetText().Lines, declaration.NameSpan),
                    },
                }
            );
        }

        return Task.FromResult(results.ToArray());
    }

    // LSP SymbolKind values.
    private static int ToLspKind(IndexedDeclarationKind kind) =>
        kind switch
        {
            IndexedDeclarationKind.Script => 5, // Class
            IndexedDeclarationKind.Function => 6, // Method
            IndexedDeclarationKind.Event or IndexedDeclarationKind.CustomEvent => 24, // Event
            IndexedDeclarationKind.Property => 7, // Property
            IndexedDeclarationKind.Variable => 8, // Field
            IndexedDeclarationKind.Struct => 23, // Struct
            IndexedDeclarationKind.StructMember => 8, // Field
            IndexedDeclarationKind.State => 10, // Enum
            IndexedDeclarationKind.Group => 3, // Namespace
            IndexedDeclarationKind.Guard => 14, // Constant
            _ => 13, // Variable
        };
}
