using System.Collections.Immutable;

namespace Papyrus.Workspaces;

public sealed class WorkspaceSnapshot
{
    public WorkspaceSnapshot(
        int projectCount,
        ImmutableArray<string> dialects,
        int openDocumentCount,
        string? lastChangeKind,
        ImmutableArray<string> projectPaths,
        ImmutableArray<string> openDocumentPaths)
    {
        ProjectCount = projectCount;
        Dialects = dialects.IsDefault ? ImmutableArray<string>.Empty : dialects;
        OpenDocumentCount = openDocumentCount;
        LastChangeKind = lastChangeKind;
        ProjectPaths = projectPaths.IsDefault ? ImmutableArray<string>.Empty : projectPaths;
        OpenDocumentPaths = openDocumentPaths.IsDefault ? ImmutableArray<string>.Empty : openDocumentPaths;
    }

    public int ProjectCount { get; }

    public ImmutableArray<string> Dialects { get; }

    public int OpenDocumentCount { get; }

    public string? LastChangeKind { get; }

    public ImmutableArray<string> ProjectPaths { get; }

    public ImmutableArray<string> OpenDocumentPaths { get; }
}
