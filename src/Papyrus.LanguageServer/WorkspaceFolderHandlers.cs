using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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

    internal WorkspaceFoldersHandler(PapyrusWorkspace workspace, Func<ILspNotifier?> getNotifier)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(getNotifier);
        _workspace = workspace;
        _getNotifier = getNotifier;
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
                    LoadProject(ppj, messages);
                }
            }
        }

        if (messages.Count > 0 && _getNotifier() is { } notifier)
        {
            foreach (var message in messages)
            {
                await notifier.NotifyAsync("window/logMessage", message, ct).ConfigureAwait(false);
            }
        }

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

    private void LoadProject(string ppj, List<LogMessageParams> messages)
    {
        PapyrusProject project;
        try
        {
            project = _workspace.LoadProject(ppj);
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
    }
}
