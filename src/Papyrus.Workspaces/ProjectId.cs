using System;

namespace Papyrus.Workspaces;

public sealed class ProjectId : IEquatable<ProjectId>
{
    private ProjectId(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static ProjectId Create(string path)
    {
        return new ProjectId(WorkspacePaths.NormalizeFull(path));
    }

    public bool Equals(ProjectId? other)
    {
        return other is not null && WorkspacePaths.EqualsFull(Path, other.Path);
    }

    public override bool Equals(object? obj) => Equals(obj as ProjectId);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);

    public override string ToString() => Path;
}
