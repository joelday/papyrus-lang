using System;
using System.Collections.Generic;
using System.IO;

namespace Papyrus.Compiler;

internal static class CorpusAcquire
{
    internal static int Execute(CorpusAcquireOptions options, TextWriter stdout, TextWriter stderr)
    {
        string source;
        string dest;
        try
        {
            source = Path.GetFullPath(options.Source);
            dest = Path.GetFullPath(options.Dest ?? Path.Combine("corpus", options.Game));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!Directory.Exists(source))
        {
            stderr.WriteLine($"error: source not found: {source}");
            return 1;
        }

        if (!options.Force && IsInsideGitWorktree(dest))
        {
            stderr.WriteLine("error: destination is inside a git worktree; pass --force to copy there anyway");
            return 1;
        }

        try
        {
            var files = ListScripts(source);
            var psc = 0;
            var pex = 0;

            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, relative);
                var targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(file, target, overwrite: true);
                if (file.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
                {
                    psc++;
                }
                else
                {
                    pex++;
                }
            }

            var noun = files.Count == 1 ? "file" : "files";
            stdout.WriteLine($"Copied {files.Count} {noun} ({psc} .psc, {pex} .pex) from {source} to {dest}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    internal static bool IsInsideGitWorktree(string path)
    {
        var full = Path.GetFullPath(path);
        // Lexical walk so a dest that does not exist yet still refuses under a worktree.
        if (HasGitAncestor(full))
        {
            return true;
        }

        var resolved = TryResolveLinkPath(full);
        return resolved is not null && HasGitAncestor(resolved);
    }

    private static bool HasGitAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (true)
        {
            var git = Path.Combine(current, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return true;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                return false;
            }

            current = parent;
        }
    }

    private static string? TryResolveLinkPath(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                return Path.GetFullPath(target.FullName);
            }

            // Dangling symlink: Exists is false but LinkTarget is still set.
            if (string.IsNullOrEmpty(info.LinkTarget))
            {
                return null;
            }

            var parent = Path.GetDirectoryName(path);
            var combined = Path.IsPathRooted(info.LinkTarget)
                ? info.LinkTarget
                : Path.Combine(parent ?? path, info.LinkTarget);
            return Path.GetFullPath(combined);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<string> ListScripts(string source)
    {
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (!IsScript(file))
            {
                continue;
            }

            var relative = Path.GetRelativePath(source, file);
            // Skip anything that escaped the source tree (symlink / junction).
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(file);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static bool IsScript(string path)
    {
        return path.EndsWith(".psc", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".pex", StringComparison.OrdinalIgnoreCase);
    }
}
