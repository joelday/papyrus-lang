using System;
using System.Collections.Immutable;

namespace Papyrus.Workspaces;

public sealed class WorkspaceChangeEventArgs : EventArgs
{
    public WorkspaceChangeEventArgs(
        PapyrusSolution oldSolution,
        PapyrusSolution newSolution,
        ImmutableArray<WorkspaceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(oldSolution);
        ArgumentNullException.ThrowIfNull(newSolution);

        OldSolution = oldSolution;
        NewSolution = newSolution;
        Changes = changes.IsDefault ? ImmutableArray<WorkspaceChange>.Empty : changes;
    }

    public PapyrusSolution OldSolution { get; }

    public PapyrusSolution NewSolution { get; }

    public ImmutableArray<WorkspaceChange> Changes { get; }
}
