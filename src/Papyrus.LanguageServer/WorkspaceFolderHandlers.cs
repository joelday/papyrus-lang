using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer;

/// <summary>
/// Workspace folder roots (file-system paths) entering and leaving the session.
/// </summary>
internal sealed record WorkspaceFolderChange(
    ImmutableArray<string> Added,
    ImmutableArray<string> Removed
)
{
    internal static WorkspaceFolderChange FromInitialize(InitializeParams? parameters)
    {
        var added = ImmutableArray.CreateBuilder<string>();
        if (parameters?.WorkspaceFolders is { } folders)
        {
            AddFolderPaths(folders, added);
        }
        else if (LspUris.TryGetFilePath(parameters?.RootUri, out var rootUriPath))
        {
            added.Add(rootUriPath);
        }
        else if (!string.IsNullOrWhiteSpace(parameters?.RootPath))
        {
            added.Add(parameters.RootPath);
        }

        return new WorkspaceFolderChange(added.ToImmutable(), ImmutableArray<string>.Empty);
    }

    internal static WorkspaceFolderChange FromDidChange(DidChangeWorkspaceFoldersParams? parameters)
    {
        var added = ImmutableArray.CreateBuilder<string>();
        var removed = ImmutableArray.CreateBuilder<string>();
        AddFolderPaths(parameters?.Event?.Added, added);
        AddFolderPaths(parameters?.Event?.Removed, removed);
        return new WorkspaceFolderChange(added.ToImmutable(), removed.ToImmutable());
    }

    private static void AddFolderPaths(
        WorkspaceFolder[]? folders,
        ImmutableArray<string>.Builder paths
    )
    {
        if (folders is null)
        {
            return;
        }

        foreach (var folder in folders)
        {
            if (LspUris.TryGetFilePath(folder?.Uri, out var path))
            {
                paths.Add(path);
            }
        }
    }
}

/// <summary>
/// Loads every <c>.ppj</c> under added workspace folders and unloads the
/// projects under removed ones. Projects are the only way a folder becomes a
/// Papyrus workspace: the <c>.ppj</c> decides the game (explicitly, or from its
/// flags file name) and every search path.
/// </summary>
internal sealed class WorkspaceFoldersHandler : ILspHandler<WorkspaceFolderChange, LspVoid>
{
    internal const string MethodName = "workspace/didChangeWorkspaceFolders";

    private readonly PapyrusWorkspace _workspace;
    private readonly Func<ILspNotifier?> _getNotifier;
    private readonly OpenBuffers _buffers;

    internal WorkspaceFoldersHandler(
        PapyrusWorkspace workspace,
        Func<ILspNotifier?> getNotifier,
        OpenBuffers? buffers = null
    )
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(getNotifier);
        _workspace = workspace;
        _getNotifier = getNotifier;
        _buffers = buffers ?? new OpenBuffers();
    }

    public string Method => MethodName;

    public bool MutatesSolution => true;

    public async Task<LspVoid> HandleAsync(
        WorkspaceFolderChange p,
        LspSnapshot snapshot,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(p);
        var messages = new List<LogMessageParams>();
        var pyroNotices = new List<ProjectNeedsPyroParams>();

        if (!p.Removed.IsDefaultOrEmpty)
        {
            UnloadProjectsUnder(snapshot.Solution, p.Removed);
        }

        if (!p.Added.IsDefaultOrEmpty)
        {
            foreach (var root in p.Added)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var ppj in ProjectDiscovery.FindProjectFiles(root))
                {
                    ProjectLoading.Load(_workspace, ppj, messages, pyroNotices, _getNotifier);
                }
            }

            _buffers.AdoptInto(_workspace);
        }

        await ProjectLoading.LogAsync(_getNotifier(), messages, pyroNotices, ct).ConfigureAwait(false);
        return LspVoid.Instance;
    }

    private void UnloadProjectsUnder(PapyrusSolution solution, ImmutableArray<string> roots)
    {
        var changes = ImmutableArray.CreateBuilder<WorkspaceChange>();
        foreach (var project in solution.Projects)
        {
            if (project.Path is not { } path)
            {
                continue;
            }

            foreach (var root in roots)
            {
                if (ProjectDiscovery.IsUnderRoot(path, root))
                {
                    changes.Add(new WorkspaceChange.FileDeleted(path));
                    break;
                }
            }
        }

        _workspace.ApplyChanges(changes.ToImmutable());
    }
}

/// <summary>Loads a <c>.ppj</c>, collecting what the client's log should say about it.</summary>
internal static class ProjectLoading
{
    internal const string ProjectNeedsPyroMethod = "papyrus/projectNeedsPyro";

    internal static void Load(
        PapyrusWorkspace workspace,
        string ppj,
        List<LogMessageParams> messages,
        List<ProjectNeedsPyroParams>? pyroNotices = null,
        Func<ILspNotifier?>? getNotifier = null)
    {
        PapyrusProject project;
        try
        {
            project = workspace.LoadProject(ppj);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or LetsPlayWithFire.Pyro.ProjectException)
        {
            messages.Add(
                new LogMessageParams
                {
                    Type = MessageType.Error,
                    Message = $"Could not load Papyrus project '{ppj}': {ex.Message}",
                }
            );
            return;
        }

        messages.Add(
            project.Dialect is null
                ? new LogMessageParams
                {
                    Type = MessageType.Warning,
                    Message =
                        $"Pyro cannot tell which game Papyrus project '{ppj}' is for; assuming Skyrim Special Edition. "
                        + "Add Game=\"sse\", \"tes5\", \"fo4\", or \"sf1\" to the project.",
                }
                : new LogMessageParams
                {
                    Type = MessageType.Log,
                    Message = $"Loaded Papyrus project '{ppj}' ({project.Game}).",
                });

        foreach (var diagnostic in project.Diagnostics)
        {
            if (diagnostic.Code == "game-type-unknown")
            {
                continue;
            }

            messages.Add(
                new LogMessageParams
                {
                    Type = diagnostic.Severity == LetsPlayWithFire.Pyro.ProjectDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning,
                    Message = $"{diagnostic.File}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Message}",
                }
            );
        }

        if (project.MissingRemotes.IsEmpty)
        {
            return;
        }

        var remotes = string.Join(", ", project.MissingRemotes);
        if (workspace.Pyro is not { } pyro)
        {
            messages.Add(
                new LogMessageParams
                {
                    Type = MessageType.Warning,
                    Message = $"Papyrus project '{ppj}' imports from remotes that Pyro downloads, and no Pyro was found: {remotes}.",
                }
            );
            pyroNotices?.Add(new ProjectNeedsPyroParams { ProjectPath = ppj, Reasons = [$"imports from remotes that Pyro downloads: {remotes}"] });
            return;
        }

        messages.Add(
            new LogMessageParams
            {
                Type = MessageType.Warning,
                Message = $"Pyro ({pyro.ExecutablePath}) has not downloaded these remote imports of '{ppj}': {remotes}. "
                    + "Build the project with Pyro once, or give Pyro an access token; it reloads here when the downloads appear.",
            }
        );
        PyroDownloadWatch.Watch(workspace, pyro, project.Id, ppj, getNotifier);
    }

    internal static async Task LogAsync(
        ILspNotifier? notifier,
        List<LogMessageParams> messages,
        List<ProjectNeedsPyroParams>? pyroNotices,
        CancellationToken ct)
    {
        if (notifier is null)
        {
            return;
        }

        foreach (var message in messages)
        {
            await notifier.NotifyAsync("window/logMessage", message, ct).ConfigureAwait(false);
        }

        foreach (var notice in pyroNotices ?? [])
        {
            await notifier.NotifyAsync(ProjectNeedsPyroMethod, notice, ct).ConfigureAwait(false);
        }
    }

    internal static Task LogAsync(ILspNotifier? notifier, List<LogMessageParams> messages, CancellationToken ct)
        => LogAsync(notifier, messages, null, ct);
}

/// <summary>
/// Reloads projects whose remote imports Pyro had not downloaded when they loaded, once
/// scripts appear under the Pyro install (that is, after the user builds with Pyro).
/// </summary>
internal sealed class PyroDownloadWatch : IDisposable
{
    private static readonly ConditionalWeakTable<PapyrusWorkspace, PyroDownloadWatch> Watches = new();

    private readonly PapyrusWorkspace _workspace;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;
    private readonly Dictionary<ProjectId, (string Ppj, Func<ILspNotifier?>? GetNotifier)> _waiting = new();

    private PyroDownloadWatch(PapyrusWorkspace workspace, PyroInstall pyro)
    {
        _workspace = workspace;
        _debounce = new Timer(_ => ReloadDownloaded(), null, Timeout.Infinite, Timeout.Infinite);

        // Watch Pyro's own folder: its "remote" folder may not exist until the first download.
        _watcher = new FileSystemWatcher(pyro.Directory, "*.psc")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;
    }

    internal static void Watch(
        PapyrusWorkspace workspace,
        PyroInstall pyro,
        ProjectId projectId,
        string ppj,
        Func<ILspNotifier?>? getNotifier)
    {
        PyroDownloadWatch watch;
        try
        {
            watch = Watches.GetValue(workspace, w => new PyroDownloadWatch(w, pyro));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return;
        }

        lock (watch._waiting)
        {
            watch._waiting[projectId] = (ppj, getNotifier);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => _debounce.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

    private void ReloadDownloaded()
    {
        KeyValuePair<ProjectId, (string Ppj, Func<ILspNotifier?>? GetNotifier)>[] waiting;
        lock (_waiting)
        {
            waiting = [.. _waiting];
        }

        foreach (var (projectId, (ppj, getNotifier)) in waiting)
        {
            var messages = new List<LogMessageParams>();
            try
            {
                if (_workspace.CurrentSolution.ContainsProject(projectId))
                {
                    _workspace.ApplyChange(new WorkspaceChange.ReloadProject(projectId));
                }

                if (!_workspace.CurrentSolution.TryGetProject(projectId, out var project) || project.MissingRemotes.IsEmpty)
                {
                    lock (_waiting)
                    {
                        _waiting.Remove(projectId);
                    }

                    if (project is not null)
                    {
                        messages.Add(new LogMessageParams { Type = MessageType.Log, Message = $"Pyro downloaded remote imports; reloaded Papyrus project '{ppj}'." });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or LetsPlayWithFire.Pyro.ProjectException or ArgumentException or ObjectDisposedException)
            {
                messages.Add(new LogMessageParams { Type = MessageType.Error, Message = $"Could not reload Papyrus project '{ppj}': {ex.Message}" });
            }

            _ = ProjectLoading.LogAsync(getNotifier?.Invoke(), messages, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}

/// <summary>
/// The text of every document the client has open, whether or not a project owns it
/// yet. A document opened before its project loads (a <c>.ppj</c> created later, or a
/// folder added later) is adopted into the project once it does, since the client
/// will not open it again.
/// </summary>
internal sealed class OpenBuffers
{
    private readonly Dictionary<string, SourceText> _buffers = new(StringComparer.OrdinalIgnoreCase);

    internal void Set(string path, SourceText text)
    {
        lock (_buffers)
        {
            _buffers[path] = text;
        }
    }

    internal void Remove(string path)
    {
        lock (_buffers)
        {
            _buffers.Remove(path);
        }
    }

    /// <summary>Opens each buffer in every project that owns its path but has not opened it.</summary>
    internal void AdoptInto(PapyrusWorkspace workspace)
    {
        var solution = workspace.CurrentSolution;
        var changes = ImmutableArray.CreateBuilder<WorkspaceChange>();
        lock (_buffers)
        {
            foreach (var (path, text) in _buffers)
            {
                foreach (var id in solution.GetDocumentIdsForPath(path))
                {
                    if (!solution.IsDocumentOpen(id))
                    {
                        changes.Add(new WorkspaceChange.OpenDocument(id, text));
                    }
                }
            }
        }

        workspace.ApplyChanges(changes.ToImmutable());
    }
}

/// <summary>
/// <c>workspace/didChangeWatchedFiles</c> for <c>**/*.ppj</c>: loads projects created
/// after startup, reloads changed ones, and unloads deleted ones. (Scripts inside loaded
/// projects are watched by the workspace itself.)
/// </summary>
internal sealed class WatchedFilesHandler : ILspHandler<DidChangeWatchedFilesParams?, LspVoid>
{
    internal const string MethodName = "workspace/didChangeWatchedFiles";

    private const int Created = 1;
    private const int Deleted = 3;

    private readonly PapyrusWorkspace _workspace;
    private readonly Func<ILspNotifier?> _getNotifier;
    private readonly OpenBuffers _buffers;

    internal WatchedFilesHandler(PapyrusWorkspace workspace, Func<ILspNotifier?> getNotifier, OpenBuffers buffers)
    {
        _workspace = workspace;
        _getNotifier = getNotifier;
        _buffers = buffers;
    }

    public string Method => MethodName;

    public bool MutatesSolution => true;

    public async Task<LspVoid> HandleAsync(DidChangeWatchedFilesParams? p, LspSnapshot snapshot, CancellationToken ct)
    {
        var messages = new List<LogMessageParams>();
        var pyroNotices = new List<ProjectNeedsPyroParams>();
        var loaded = false;
        foreach (var change in p?.Changes ?? [])
        {
            if (!LspUris.TryGetFilePath(change?.Uri, out var path)
                || !Path.GetExtension(path).Equals(".ppj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = ProjectId.Create(Path.GetFullPath(path));
            var isLoaded = _workspace.CurrentSolution.ContainsProject(id);
            if (change!.Type == Deleted)
            {
                if (isLoaded)
                {
                    _workspace.ApplyChange(new WorkspaceChange.FileDeleted(path));
                    messages.Add(new LogMessageParams { Type = MessageType.Log, Message = $"Unloaded Papyrus project '{path}'." });
                }
            }
            else if (isLoaded && change.Type != Created)
            {
                _workspace.ApplyChange(new WorkspaceChange.ReloadProject(id));
            }
            else if (File.Exists(path))
            {
                ProjectLoading.Load(_workspace, path, messages, pyroNotices, _getNotifier);
                loaded = true;
            }
        }

        if (loaded)
        {
            _buffers.AdoptInto(_workspace);
        }

        await ProjectLoading.LogAsync(_getNotifier(), messages, pyroNotices, ct).ConfigureAwait(false);
        return LspVoid.Instance;
    }
}
