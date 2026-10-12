using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
                    ProjectLoading.Load(_workspace, ppj, messages);
                }
            }

            _buffers.AdoptInto(_workspace);
        }

        await ProjectLoading.LogAsync(_getNotifier(), messages, ct).ConfigureAwait(false);
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
    internal static void Load(PapyrusWorkspace workspace, string ppj, List<LogMessageParams> messages)
    {
        PapyrusProject project;
        try
        {
            project = workspace.LoadProject(ppj);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or XmlException
                        or InvalidDataException
            )
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

        if (project.Dialect is null)
        {
            messages.Add(
                new LogMessageParams
                {
                    Type = MessageType.Warning,
                    Message =
                        $"Papyrus project '{ppj}' has no Game attribute and its Flags file does not identify a game; "
                        + "assuming Skyrim Special Edition. Add Game=\"sse\", \"tesv\", \"fo4\", or \"starfield\" to the project.",
                }
            );
        }
        else
        {
            messages.Add(
                new LogMessageParams
                {
                    Type = MessageType.Log,
                    Message = $"Loaded Papyrus project '{ppj}' ({project.Game}).",
                }
            );
        }

        if (!project.Ignored.IsEmpty)
        {
            messages.Add(
                new LogMessageParams
                {
                    Type = MessageType.Info,
                    Message = IgnoredProjectContent.Describe(ppj, project.Ignored),
                }
            );
        }
    }

    internal static async Task LogAsync(ILspNotifier? notifier, List<LogMessageParams> messages, CancellationToken ct)
    {
        if (notifier is null)
        {
            return;
        }

        foreach (var message in messages)
        {
            await notifier.NotifyAsync("window/logMessage", message, ct).ConfigureAwait(false);
        }
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
                ProjectLoading.Load(_workspace, path, messages);
                loaded = true;
            }
        }

        if (loaded)
        {
            _buffers.AdoptInto(_workspace);
        }

        await ProjectLoading.LogAsync(_getNotifier(), messages, ct).ConfigureAwait(false);
        return LspVoid.Instance;
    }
}
