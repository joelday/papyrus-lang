using System;
using System.IO;

namespace Papyrus.Architecture.Tests;

internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Papyrus.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate Papyrus.sln starting from " + AppContext.BaseDirectory + ".");
    }
}
