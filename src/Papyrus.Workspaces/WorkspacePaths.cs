using System;
using System.IO;

namespace Papyrus.Workspaces;

internal static class WorkspacePaths
{
    public static string NormalizeFull(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    public static bool EqualsFull(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetExtension(string path)
    {
        return Path.GetExtension(path);
    }

    public static bool HasExtension(string path, string extension)
    {
        return GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);
    }
}
