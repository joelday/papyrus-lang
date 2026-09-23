using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace Papyrus.Workspaces;

/// <summary>
/// Finds the <c>.ppj</c> project files under a workspace root. A folder is only
/// ever a Papyrus workspace through the projects it contains; there is no
/// ambient (project-less) mode.
/// </summary>
public static class ProjectDiscovery
{
    // Directories that never hold a user's projects but can be large enough to make
    // a scan of the repository root noticeably slow.
    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        "bin",
        "obj",
    };

    /// <summary>
    /// Returns every <c>.ppj</c> under <paramref name="root"/>, sorted by full path.
    /// Hidden directories (<c>.git</c>, <c>.vscode</c>, ...), build output, and
    /// symlinked directories are not descended into. Missing or unreadable
    /// directories contribute nothing.
    /// </summary>
    public static ImmutableArray<string> FindProjectFiles(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var results = ImmutableArray.CreateBuilder<string>();
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full))
        {
            return results.ToImmutable();
        }

        var pending = new Stack<string>();
        pending.Push(full);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in EnumerateEntries(directory))
            {
                if (entry is FileInfo file)
                {
                    if (WorkspacePaths.HasExtension(file.Name, ".ppj"))
                    {
                        results.Add(file.FullName);
                    }
                }
                else if (entry is DirectoryInfo child && ShouldDescend(child))
                {
                    pending.Push(child.FullName);
                }
            }
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results.ToImmutable();
    }

    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="root"/> or lies beneath it.
    /// </summary>
    public static bool IsUnderRoot(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var fullPath = WorkspacePaths.NormalizeFull(path);
        var fullRoot = WorkspacePaths.NormalizeFull(root).TrimEnd('/');
        return WorkspacePaths.EqualsFull(fullPath, fullRoot)
            || fullPath.StartsWith(fullRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static FileSystemInfo[] EnumerateEntries(string directory)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0,
        };

        try
        {
            return new DirectoryInfo(directory).GetFileSystemInfos("*", options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The directory vanished or became unreadable between listing and descent;
            // it simply contributes no projects.
            return Array.Empty<FileSystemInfo>();
        }
    }

    private static bool ShouldDescend(DirectoryInfo directory)
    {
        return !directory.Name.StartsWith('.')
            && !SkippedDirectoryNames.Contains(directory.Name)
            && (directory.Attributes & FileAttributes.ReparsePoint) == 0;
    }
}
