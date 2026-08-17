using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.Workspaces;

public sealed class PapyrusSolution
{
    public static PapyrusSolution Empty { get; } = new(
        ImmutableDictionary<ProjectId, ProjectState>.Empty,
        ImmutableDictionary<DocumentId, SourceText>.Empty);

    private readonly ImmutableDictionary<ProjectId, ProjectState> _projects;
    private readonly ImmutableDictionary<DocumentId, SourceText> _openDocuments;

    private PapyrusSolution(
        ImmutableDictionary<ProjectId, ProjectState> projects,
        ImmutableDictionary<DocumentId, SourceText> openDocuments)
    {
        _projects = projects;
        _openDocuments = openDocuments;

        var projectIds = ImmutableArray.CreateBuilder<ProjectId>(projects.Count);
        foreach (var pair in projects)
        {
            projectIds.Add(pair.Key);
        }

        ProjectIds = projectIds.ToImmutable();

        var documentIds = ImmutableArray.CreateBuilder<DocumentId>(openDocuments.Count);
        foreach (var pair in openDocuments)
        {
            documentIds.Add(pair.Key);
        }

        OpenDocumentIds = documentIds.ToImmutable();
    }

    public ImmutableArray<ProjectId> ProjectIds { get; }

    public ImmutableArray<DocumentId> OpenDocumentIds { get; }

    public IEnumerable<PapyrusProject> Projects
    {
        get
        {
            foreach (var pair in _projects)
            {
                yield return pair.Value.Project;
            }
        }
    }

    public PapyrusCompilation? Compilation
    {
        get
        {
            foreach (var pair in _projects)
            {
                return pair.Value.Compilation;
            }

            return null;
        }
    }

    public bool ContainsProject(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        return _projects.ContainsKey(projectId);
    }

    public PapyrusProject GetProject(ProjectId projectId)
    {
        return GetState(projectId).Project;
    }

    public bool TryGetProject(ProjectId projectId, out PapyrusProject project)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (_projects.TryGetValue(projectId, out var state))
        {
            project = state.Project;
            return true;
        }

        project = null!;
        return false;
    }

    public PapyrusCompilation GetCompilation(ProjectId projectId)
    {
        return GetState(projectId).Compilation;
    }

    public bool IsDocumentOpen(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return _openDocuments.ContainsKey(documentId);
    }

    public bool TryGetOpenDocumentText(DocumentId documentId, out SourceText text)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        return _openDocuments.TryGetValue(documentId, out text!);
    }

    public WorkspaceSnapshot Describe() => Describe(lastChangeKind: null);

    internal WorkspaceSnapshot Describe(string? lastChangeKind)
    {
        var dialects = ImmutableArray.CreateBuilder<string>(_projects.Count);
        var projectPaths = ImmutableArray.CreateBuilder<string>(_projects.Count);
        foreach (var pair in _projects)
        {
            var dialect = pair.Value.Project.Dialect;
            dialects.Add(dialect is null ? "Unknown" : dialect.Game.ToString());
            if (pair.Value.Project.Path is { } path)
            {
                projectPaths.Add(path);
            }
        }

        var documentPaths = ImmutableArray.CreateBuilder<string>(_openDocuments.Count);
        foreach (var pair in _openDocuments)
        {
            documentPaths.Add(pair.Key.Path);
        }

        return new WorkspaceSnapshot(
            _projects.Count,
            dialects.ToImmutable(),
            _openDocuments.Count,
            lastChangeKind,
            projectPaths.ToImmutable(),
            documentPaths.ToImmutable());
    }

    internal ImmutableDictionary<DocumentId, SourceText> OpenDocuments => _openDocuments;

    internal ProjectState GetState(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (!_projects.TryGetValue(projectId, out var state))
        {
            throw new ArgumentException("Project is not in the solution.", nameof(projectId));
        }

        return state;
    }

    internal PapyrusSolution WithProject(ProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_projects.TryGetValue(state.Id, out var existing) && ReferenceEquals(existing, state))
        {
            return this;
        }

        return new PapyrusSolution(_projects.SetItem(state.Id, state), _openDocuments);
    }

    internal PapyrusSolution WithOpenDocument(DocumentId documentId, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(text);
        if (_openDocuments.TryGetValue(documentId, out var existing) && ReferenceEquals(existing, text))
        {
            return this;
        }

        return new PapyrusSolution(_projects, _openDocuments.SetItem(documentId, text));
    }

    internal PapyrusSolution WithoutOpenDocument(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        if (!_openDocuments.ContainsKey(documentId))
        {
            return this;
        }

        return new PapyrusSolution(_projects, _openDocuments.Remove(documentId));
    }

    internal PapyrusSolution WithoutProject(ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        if (!_projects.ContainsKey(projectId))
        {
            return this;
        }

        var openDocuments = _openDocuments;
        foreach (var pair in _openDocuments)
        {
            if (pair.Key.ProjectId.Equals(projectId))
            {
                openDocuments = openDocuments.Remove(pair.Key);
            }
        }

        return new PapyrusSolution(_projects.Remove(projectId), openDocuments);
    }
}

internal sealed class ProjectState
{
    public ProjectState(PapyrusProject project, FileSystemScriptResolver diskResolver, PapyrusCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(diskResolver);
        ArgumentNullException.ThrowIfNull(compilation);
        if (project.Path is null)
        {
            throw new ArgumentException("Project has no path.", nameof(project));
        }

        Id = ProjectId.Create(project.Path);
        Project = project;
        DiskResolver = diskResolver;
        Compilation = compilation;
    }

    public ProjectId Id { get; }

    public PapyrusProject Project { get; }

    public FileSystemScriptResolver DiskResolver { get; }

    public PapyrusCompilation Compilation { get; }
}
