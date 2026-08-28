using System.Threading;
using System.Threading.Tasks;

namespace Papyrus.LanguageServer;

/// <summary>
/// Disassembles a document's compiled script into Papyrus assembly (.pas) text.
/// Reimplements the legacy <c>textDocument/assembly</c> handler against
/// <see cref="Workspaces.PapyrusSolution"/>, which owns emitting via
/// <see cref="CodeAnalysis.Emit.PexEmitter"/> and disassembling via
/// <see cref="Pex.PasDisassembler"/> so the LSP layer stays thin.
/// </summary>
internal sealed class DocumentAssemblyHandler : ILspHandler<DocumentAssemblyParams?, DocumentAssembly>
{
    internal const string MethodName = "textDocument/assembly";

    public string Method => MethodName;

    public bool MutatesSolution => false;

    public Task<DocumentAssembly> HandleAsync(
        DocumentAssemblyParams? p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (!LspDocuments.TryGetDocumentId(snapshot, p?.TextDocument, out var documentId))
        {
            return Task.FromResult(new DocumentAssembly());
        }

        return Task.FromResult(
            new DocumentAssembly { Assembly = snapshot.Solution.GetAssembly(documentId, ct) }
        );
    }
}
