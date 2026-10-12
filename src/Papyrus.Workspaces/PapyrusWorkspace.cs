using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.Workspaces;

public sealed class PapyrusWorkspace : IDisposable
{
    public static readonly TimeSpan DefaultWatchDebounce = TimeSpan.FromMilliseconds(250);

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _watchDebounce;
    private readonly bool _enableFileWatching;
    private readonly ILogger? _logger;
    private readonly List<WorkspaceChange> _pendingWatch = new();
    private readonly List<FileSystemWatcher> _watchers = new();

    private PapyrusSolution _solution;
    private ITimer? _watchTimer;
    private DateTimeOffset _lastWatchUtc;
    private string? _lastChangeKind;
    private bool _disposed;

    public PapyrusWorkspace()
        : this(TimeProvider.System, DefaultWatchDebounce, enableFileWatching: true)
    {
    }

    public PapyrusWorkspace(
        TimeProvider timeProvider,
        TimeSpan? watchDebounce = null,
        bool enableFileWatching = true,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var debounce = watchDebounce ?? DefaultWatchDebounce;
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(watchDebounce));
        }

        _timeProvider = timeProvider;
        _watchDebounce = debounce;
        _enableFileWatching = enableFileWatching;
        _logger = logger;
        _solution = PapyrusSolution.Empty;
    }

    public PapyrusSolution CurrentSolution
    {
        get
        {
            lock (_gate)
            {
                return _solution;
            }
        }
    }

    public event EventHandler<WorkspaceChangeEventArgs>? WorkspaceChanged;

    public WorkspaceSnapshot Describe()
    {
        lock (_gate)
        {
            return _solution.Describe(_lastChangeKind);
        }
    }

    public PapyrusProject LoadProject(string ppjPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppjPath);

        var project = ReadProject(ppjPath);
        var id = project.Id;
        ApplyLoadedProject(project);
        return CurrentSolution.GetProject(id);
    }

    public void ApplyChange(WorkspaceChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ApplyChanges(ImmutableArray.Create(change));
    }

    public void ApplyChanges(ImmutableArray<WorkspaceChange> changes)
    {
        if (changes.IsDefaultOrEmpty)
        {
            return;
        }

        ApplyChangesCore(changes);
    }

    public void NotifyWatchEvent(WorkspaceChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var flushImmediately = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pendingWatch.Add(change);
            _lastWatchUtc = _timeProvider.GetUtcNow();
            if (_watchDebounce == TimeSpan.Zero)
            {
                flushImmediately = true;
            }
            else
            {
                ArmWatchTimer_NoLock();
            }
        }

        if (flushImmediately)
        {
            FlushPendingWatchEvents();
        }
    }

    public void FlushPendingWatchEvents()
    {
        ImmutableArray<WorkspaceChange> batch;
        lock (_gate)
        {
            if (_pendingWatch.Count == 0)
            {
                return;
            }

            batch = Coalesce(_pendingWatch);
            _pendingWatch.Clear();
            DisarmWatchTimer_NoLock();
        }

        ApplyChangesCore(batch);
    }

    public void FlushWatchEventsIfDebounceElapsed()
    {
        lock (_gate)
        {
            if (_pendingWatch.Count == 0)
            {
                return;
            }

            if (_timeProvider.GetUtcNow() - _lastWatchUtc < _watchDebounce)
            {
                return;
            }
        }

        FlushPendingWatchEvents();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingWatch.Clear();
            _watchTimer?.Dispose();
            _watchTimer = null;
            DisposeWatchers_NoLock();
        }
    }

    private void ApplyLoadedProject(PapyrusProject project)
    {
        PapyrusSolution oldSolution;
        PapyrusSolution newSolution;
        ImmutableArray<string>? watchDirectories = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            oldSolution = _solution;
            newSolution = oldSolution.WithProject(CreateProjectState(project, oldSolution.OpenDocuments, _logger));
            _solution = newSolution;
            _lastChangeKind = nameof(WorkspaceChange.ReloadProject);
            if (_enableFileWatching)
            {
                watchDirectories = CollectWatchDirectories(newSolution);
            }
        }

        if (watchDirectories.HasValue)
        {
            ReplaceWatchers(watchDirectories.Value);
        }

        if (!ReferenceEquals(oldSolution, newSolution))
        {
            WorkspaceChanged?.Invoke(
                this,
                new WorkspaceChangeEventArgs(
                    oldSolution,
                    newSolution,
                    ImmutableArray.Create<WorkspaceChange>(new WorkspaceChange.ReloadProject(project.Id))));
        }
    }

    private void ApplyChangesCore(ImmutableArray<WorkspaceChange> changes)
    {
        if (changes.IsDefaultOrEmpty)
        {
            return;
        }

        var lastChange = changes[^1];
        using var activity = PapyrusActivitySource.Start(
            "workspace.applyChange",
            ApplyChangeTags(lastChange, changes.Length));
        PapyrusEventSource.Log.ApplyChange();
        _logger?.LogDebug(
            "ApplyChange {ChangeKind} Activity={ActivityId}",
            lastChange.GetType().Name,
            Activity.Current?.Id);

        PapyrusSolution oldSolution;
        PapyrusSolution newSolution;
        ImmutableArray<string>? watchDirectories = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            oldSolution = _solution;
            newSolution = oldSolution;
            for (var i = 0; i < changes.Length; i++)
            {
                newSolution = ApplyCore(newSolution, changes[i]);
            }

            if (ReferenceEquals(oldSolution, newSolution))
            {
                return;
            }

            _solution = newSolution;
            _lastChangeKind = lastChange.GetType().Name;
            if (_enableFileWatching)
            {
                watchDirectories = CollectWatchDirectories(newSolution);
            }
        }

        if (watchDirectories.HasValue)
        {
            ReplaceWatchers(watchDirectories.Value);
        }

        WorkspaceChanged?.Invoke(this, new WorkspaceChangeEventArgs(oldSolution, newSolution, changes));
    }

    private PapyrusSolution ApplyCore(PapyrusSolution solution, WorkspaceChange change)
    {
        return change switch
        {
            WorkspaceChange.OpenDocument open => OpenOrChangeDocument(solution, open.Id, open.Text, _logger),
            WorkspaceChange.ChangeDocument edit => OpenOrChangeDocument(solution, edit.Id, edit.Text, _logger),
            WorkspaceChange.CloseDocument close => CloseDocument(solution, close.Id, _logger),
            WorkspaceChange.ReloadProject reload => ReloadProject(solution, reload.Id, _logger),
            WorkspaceChange.FileCreated created => ApplyFileCreated(solution, created.Path, _logger),
            WorkspaceChange.FileDeleted deleted => ApplyFileDeleted(solution, deleted.Path, _logger),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
    }

    private static PapyrusSolution OpenOrChangeDocument(
        PapyrusSolution solution,
        DocumentId id,
        SourceText text,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(text);
        if (!solution.ContainsProject(id.ProjectId))
        {
            throw new ArgumentException("Project is not loaded.", nameof(id));
        }

        if (solution.TryGetOpenDocumentText(id, out var existing) && ReferenceEquals(existing, text))
        {
            return solution;
        }

        var next = solution.WithOpenDocument(id, text);
        return ApplyScriptText(next, id.ProjectId, id.Path, text, logger);
    }

    private static PapyrusSolution CloseDocument(PapyrusSolution solution, DocumentId id, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!solution.IsDocumentOpen(id))
        {
            return solution;
        }

        var next = solution.WithoutOpenDocument(id);
        if (!next.ContainsProject(id.ProjectId))
        {
            return next;
        }

        return InvalidateScriptPath(next, id.ProjectId, id.Path, logger);
    }

    private static PapyrusSolution ReloadProject(PapyrusSolution solution, ProjectId id, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!solution.TryGetProject(id, out var existing))
        {
            throw new ArgumentException("Project is not loaded.", nameof(id));
        }

        var path = existing.Path ?? id.Path;
        return solution.WithProject(CreateProjectState(ReadProject(path), solution.OpenDocuments, logger));
    }

    private static PapyrusSolution ApplyFileCreated(PapyrusSolution solution, string path, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (TryGetLoadedProjectId(solution, path, out var projectId))
        {
            return ReloadProject(solution, projectId, logger);
        }

        if (WorkspacePaths.HasExtension(path, ".psc"))
        {
            return ApplyScriptFileChange(solution, path, deleted: false, logger);
        }

        return RebuildCompilations(solution, logger);
    }

    private static PapyrusSolution ApplyFileDeleted(PapyrusSolution solution, string path, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (TryGetLoadedProjectId(solution, path, out var projectId))
        {
            return solution.WithoutProject(projectId);
        }

        if (WorkspacePaths.HasExtension(path, ".psc"))
        {
            return ApplyScriptFileChange(solution, path, deleted: true, logger);
        }

        return RebuildCompilations(solution, logger);
    }

    private static PapyrusSolution ApplyScriptFileChange(
        PapyrusSolution solution,
        string path,
        bool deleted,
        ILogger? logger)
    {
        var next = solution;
        var mapped = false;
        var projectIds = solution.ProjectIds;
        for (var i = 0; i < projectIds.Length; i++)
        {
            var projectId = projectIds[i];
            var state = next.GetState(projectId);
            if (!state.DiskResolver.TryGetNameFromPath(path, out var name))
            {
                continue;
            }

            // A script appeared or disappeared (or changed): the cached script list may be stale.
            state.DiskResolver.InvalidateScripts();
            mapped = true;
            if (TryGetOpenDocumentText(next, projectId, path, out var openText))
            {
                if (deleted)
                {
                    continue;
                }

                next = next.WithProject(WithCompilation(state, state.Compilation.WithScriptText(name, openText)));
                continue;
            }

            next = next.WithProject(WithCompilation(state, state.Compilation.WithoutScript(name)));
        }

        return mapped ? next : RebuildCompilations(solution, logger);
    }

    private static PapyrusSolution ApplyScriptText(
        PapyrusSolution solution,
        ProjectId projectId,
        string path,
        SourceText text,
        ILogger? logger)
    {
        var state = solution.GetState(projectId);
        if (!state.DiskResolver.TryGetNameFromPath(path, out var name))
        {
            return solution.WithProject(CreateProjectState(state.Project, solution.OpenDocuments, logger));
        }

        return solution.WithProject(WithCompilation(state, state.Compilation.WithScriptText(name, text)));
    }

    private static PapyrusSolution InvalidateScriptPath(
        PapyrusSolution solution,
        ProjectId projectId,
        string path,
        ILogger? logger)
    {
        var state = solution.GetState(projectId);
        if (!state.DiskResolver.TryGetNameFromPath(path, out var name))
        {
            return solution.WithProject(CreateProjectState(state.Project, solution.OpenDocuments, logger));
        }

        return solution.WithProject(WithCompilation(state, state.Compilation.WithoutScript(name)));
    }

    private static bool TryGetOpenDocumentText(
        PapyrusSolution solution,
        ProjectId projectId,
        string path,
        out SourceText text)
    {
        return solution.TryGetOpenDocumentText(DocumentId.Create(projectId, path), out text);
    }

    private static ProjectState WithCompilation(ProjectState state, PapyrusCompilation compilation)
    {
        if (ReferenceEquals(state.Compilation, compilation))
        {
            return state;
        }

        return new ProjectState(state.Project, state.DiskResolver, compilation);
    }

    private static PapyrusSolution RebuildCompilations(PapyrusSolution solution, ILogger? logger)
    {
        if (solution.ProjectIds.IsDefaultOrEmpty)
        {
            return solution;
        }

        var next = solution;
        var projectIds = solution.ProjectIds;
        for (var i = 0; i < projectIds.Length; i++)
        {
            var project = next.GetProject(projectIds[i]);
            next = next.WithProject(CreateProjectState(project, next.OpenDocuments, logger));
        }

        return next;
    }

    internal static ProjectState CreateProjectState(
        PapyrusProject project,
        ImmutableDictionary<DocumentId, SourceText> openDocuments,
        ILogger? logger = null)
    {
        if (project.Path is null)
        {
            throw new ArgumentException("Project has no path.", nameof(project));
        }

        var projectDirectory = Path.GetDirectoryName(project.Path);
        if (string.IsNullOrEmpty(projectDirectory))
        {
            throw new ArgumentException("Project path has no directory.", nameof(project));
        }

        var disk = new FileSystemScriptResolver(project, projectDirectory);
        var resolver = OverlayOpenDocuments(disk, project.Id, openDocuments);
        var dialect = project.Dialect ?? PapyrusDialect.SkyrimSe;
        var compilation = PapyrusCompilation.Create(dialect, resolver, logger: logger);
        return new ProjectState(project, disk, compilation);
    }

    private static ScriptResolver OverlayOpenDocuments(
        FileSystemScriptResolver disk,
        ProjectId projectId,
        ImmutableDictionary<DocumentId, SourceText> openDocuments)
    {
        List<KeyValuePair<string, SourceText>>? pathBuffers = null;
        foreach (var pair in openDocuments)
        {
            if (!pair.Key.ProjectId.Equals(projectId))
            {
                continue;
            }

            pathBuffers ??= new List<KeyValuePair<string, SourceText>>();
            pathBuffers.Add(new KeyValuePair<string, SourceText>(pair.Key.Path, pair.Value));
        }

        if (pathBuffers is null)
        {
            return disk;
        }

        return new BufferScriptResolver(disk, pathBuffers);
    }

    private static PapyrusProject ReadProject(string ppjPath)
    {
        var full = Path.GetFullPath(ppjPath);
        using var stream = File.OpenRead(full);
        return PpjParser.Parse(stream).WithPath(full);
    }

    private static bool TryGetLoadedProjectId(PapyrusSolution solution, string path, out ProjectId projectId)
    {
        if (!WorkspacePaths.HasExtension(path, ".ppj"))
        {
            projectId = null!;
            return false;
        }

        projectId = ProjectId.Create(path);
        return solution.ContainsProject(projectId);
    }

    private static ImmutableArray<WorkspaceChange> Coalesce(List<WorkspaceChange> pending)
    {
        var lastByKey = new Dictionary<string, WorkspaceChange>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        for (var i = 0; i < pending.Count; i++)
        {
            var change = pending[i];
            var key = ChangeKey(change);
            if (lastByKey.ContainsKey(key))
            {
                lastByKey[key] = change;
                continue;
            }

            lastByKey[key] = change;
            order.Add(key);
        }

        var builder = ImmutableArray.CreateBuilder<WorkspaceChange>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            builder.Add(lastByKey[order[i]]);
        }

        return builder.MoveToImmutable();
    }

    private static KeyValuePair<string, object?>[] ApplyChangeTags(WorkspaceChange change, int count)
    {
        var kind = change.GetType().Name;
        var path = change switch
        {
            WorkspaceChange.OpenDocument open => open.Id.Path,
            WorkspaceChange.ChangeDocument edit => edit.Id.Path,
            WorkspaceChange.CloseDocument close => close.Id.Path,
            WorkspaceChange.FileCreated created => created.Path,
            WorkspaceChange.FileDeleted deleted => deleted.Path,
            WorkspaceChange.ReloadProject reload => reload.Id.Path,
            _ => null,
        };

        return path is null
            ? [new("change.kind", kind), new("change.count", count)]
            : [new("change.kind", kind), new("change.count", count), new("document", path)];
    }

    private static string ChangeKey(WorkspaceChange change)
    {
        return change switch
        {
            WorkspaceChange.OpenDocument open => "doc:" + open.Id.ProjectId.Path + "|" + open.Id.Path,
            WorkspaceChange.ChangeDocument edit => "doc:" + edit.Id.ProjectId.Path + "|" + edit.Id.Path,
            WorkspaceChange.CloseDocument close => "doc:" + close.Id.ProjectId.Path + "|" + close.Id.Path,
            WorkspaceChange.ReloadProject reload => "proj:" + reload.Id.Path,
            WorkspaceChange.FileCreated created => "file:" + WorkspacePaths.NormalizeFull(created.Path),
            WorkspaceChange.FileDeleted deleted => "file:" + WorkspacePaths.NormalizeFull(deleted.Path),
            _ => "other:" + change.GetType().FullName,
        };
    }

    private void OnWatchTimer()
    {
        if (_disposed)
        {
            return;
        }

        FlushWatchEventsIfDebounceElapsed();
    }

    private void ArmWatchTimer_NoLock()
    {
        if (_disposed)
        {
            return;
        }

        _watchTimer ??= _timeProvider.CreateTimer(
            static state => ((PapyrusWorkspace)state!).OnWatchTimer(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _watchTimer.Change(_watchDebounce, Timeout.InfiniteTimeSpan);
    }

    private void DisarmWatchTimer_NoLock()
    {
        _watchTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal static WorkspaceChange? ClassifyWatchEvent(
        WatcherChangeTypes changeType,
        string path,
        PapyrusSolution solution)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (WorkspacePaths.HasExtension(path, ".ppj"))
        {
            if (TryGetLoadedProjectId(solution, path, out var projectId))
            {
                return changeType == WatcherChangeTypes.Deleted
                    ? new WorkspaceChange.FileDeleted(path)
                    : new WorkspaceChange.ReloadProject(projectId);
            }

            return changeType == WatcherChangeTypes.Deleted
                ? new WorkspaceChange.FileDeleted(path)
                : new WorkspaceChange.FileCreated(path);
        }

        if (WorkspacePaths.HasExtension(path, ".psc")
            || WorkspacePaths.HasExtension(path, ".pex")
            || WorkspacePaths.HasExtension(path, ".flg"))
        {
            return changeType == WatcherChangeTypes.Deleted
                ? new WorkspaceChange.FileDeleted(path)
                : new WorkspaceChange.FileCreated(path);
        }

        return null;
    }

    private void OnWatcherCreated(object sender, FileSystemEventArgs e)
    {
        EnqueueWatcherEvent(WatcherChangeTypes.Created, e.FullPath);
    }

    private void OnWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        EnqueueWatcherEvent(WatcherChangeTypes.Deleted, e.FullPath);
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        EnqueueWatcherEvent(WatcherChangeTypes.Changed, e.FullPath);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        EnqueueWatcherEvent(WatcherChangeTypes.Deleted, e.OldFullPath);
        EnqueueWatcherEvent(WatcherChangeTypes.Created, e.FullPath);
    }

    private void EnqueueWatcherEvent(WatcherChangeTypes changeType, string path)
    {
        WorkspaceChange? change;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            change = ClassifyWatchEvent(changeType, path, _solution);
        }

        if (change is not null)
        {
            NotifyWatchEvent(change);
        }
    }

    private static ImmutableArray<string> CollectWatchDirectories(PapyrusSolution solution)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectId in solution.ProjectIds)
        {
            var state = solution.GetState(projectId);
            if (state.Project.Path is not null)
            {
                var projectDirectory = Path.GetDirectoryName(state.Project.Path);
                if (!string.IsNullOrEmpty(projectDirectory))
                {
                    directories.Add(Path.GetFullPath(projectDirectory));
                }
            }

            var includes = state.DiskResolver.IncludeDirectories;
            for (var i = 0; i < includes.Length; i++)
            {
                directories.Add(includes[i]);
            }
        }

        var builder = ImmutableArray.CreateBuilder<string>(directories.Count);
        foreach (var directory in directories)
        {
            builder.Add(directory);
        }

        return builder.ToImmutable();
    }

    private void ReplaceWatchers(ImmutableArray<string> directories)
    {
        List<FileSystemWatcher> created = new();
        try
        {
            for (var i = 0; i < directories.Length; i++)
            {
                var directory = directories[i];
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                    Filter = "*.*",
                    InternalBufferSize = 64 * 1024,
                };
                watcher.Created += OnWatcherCreated;
                watcher.Deleted += OnWatcherDeleted;
                watcher.Changed += OnWatcherChanged;
                watcher.Renamed += OnWatcherRenamed;
                watcher.EnableRaisingEvents = true;
                created.Add(watcher);
            }
        }
        catch
        {
            for (var i = 0; i < created.Count; i++)
            {
                created[i].Dispose();
            }

            throw;
        }

        lock (_gate)
        {
            DisposeWatchers_NoLock();
            if (_disposed)
            {
                for (var i = 0; i < created.Count; i++)
                {
                    created[i].Dispose();
                }

                return;
            }

            _watchers.AddRange(created);
        }
    }

    private void DisposeWatchers_NoLock()
    {
        for (var i = 0; i < _watchers.Count; i++)
        {
            _watchers[i].Dispose();
        }

        _watchers.Clear();
    }
}
