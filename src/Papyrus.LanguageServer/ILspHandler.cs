using System;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

internal readonly struct LspSnapshot
{
    public LspSnapshot(PapyrusSolution solution, int generation)
    {
        ArgumentNullException.ThrowIfNull(solution);
        Solution = solution;
        Generation = generation;
    }

    public PapyrusSolution Solution { get; }

    public int Generation { get; }
}

internal interface ILspHandler<TParams, TResult>
{
    string Method { get; }

    bool MutatesSolution { get; }

    Task<TResult> HandleAsync(TParams p, LspSnapshot snapshot, CancellationToken ct);
}
