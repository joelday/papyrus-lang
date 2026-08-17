using System;

namespace Papyrus.Workspaces;

public sealed class DocumentId : IEquatable<DocumentId>
{
    private DocumentId(ProjectId projectId, string path)
    {
        ProjectId = projectId;
        Path = path;
    }

    public ProjectId ProjectId { get; }

    public string Path { get; }

    public static DocumentId Create(ProjectId projectId, string path)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        return new DocumentId(projectId, WorkspacePaths.NormalizeFull(path));
    }

    public bool Equals(DocumentId? other)
    {
        return other is not null
            && ProjectId.Equals(other.ProjectId)
            && WorkspacePaths.EqualsFull(Path, other.Path);
    }

    public override bool Equals(object? obj) => Equals(obj as DocumentId);

    public override int GetHashCode()
    {
        return HashCode.Combine(ProjectId, StringComparer.OrdinalIgnoreCase.GetHashCode(Path));
    }

    public override string ToString() => Path;
}
