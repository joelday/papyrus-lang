using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal interface ILspHandler<TParams, TResult>
{
    string Method { get; }

    bool MutatesSolution { get; }

    Task<TResult> HandleAsync(TParams p, PapyrusSolution solution, CancellationToken ct);
}
