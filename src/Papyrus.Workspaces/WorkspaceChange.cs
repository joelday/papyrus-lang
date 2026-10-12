using Papyrus.CodeAnalysis.Text;

namespace Papyrus.Workspaces;

public abstract record WorkspaceChange
{
    private WorkspaceChange()
    {
    }

    public sealed record OpenDocument(DocumentId Id, SourceText Text) : WorkspaceChange;

    public sealed record ChangeDocument(DocumentId Id, SourceText Text) : WorkspaceChange;

    public sealed record CloseDocument(DocumentId Id) : WorkspaceChange;

    public sealed record ReloadProject(ProjectId Id) : WorkspaceChange;

    public sealed record FileCreated(string Path) : WorkspaceChange;

    public sealed record FileDeleted(string Path) : WorkspaceChange;
}
