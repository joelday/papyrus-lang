using System;
using System.Collections.Generic;
using System.IO;
using Papyrus.CodeAnalysis;

namespace Papyrus.CodeAnalysis.Testing;

public static class ParseFixtureCatalog
{
    public static IReadOnlyList<ParseFixture> Load(string fixturesDirectory)
    {
        if (string.IsNullOrEmpty(fixturesDirectory) || !Directory.Exists(fixturesDirectory))
        {
            throw new DirectoryNotFoundException(
                "Parse fixture directory was not found: " + fixturesDirectory);
        }

        var fixtures = new List<ParseFixture>();
        foreach (var path in Directory.EnumerateFiles(fixturesDirectory, "*.psc", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixturesDirectory, path).Replace('\\', '/');
            var source = File.ReadAllText(path);
            fixtures.Add(new ParseFixture(relative, source, DialectFromRelativePath(relative)));
        }

        fixtures.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        if (fixtures.Count == 0)
        {
            throw new InvalidOperationException(
                "No .psc parse fixtures were found under " + fixturesDirectory + ".");
        }

        return fixtures;
    }

    public static string FindDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var output = Path.Combine(dir.FullName, "Fixtures", "Parse");
            if (Directory.Exists(output))
            {
                return output;
            }

            var source = Path.Combine(dir.FullName, "tests", "Papyrus.CodeAnalysis.Tests", "Fixtures", "Parse");
            if (Directory.Exists(source))
            {
                return source;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate Fixtures/Parse starting from " + AppContext.BaseDirectory + ".");
    }

    internal static PapyrusDialect DialectFromRelativePath(string relativePath)
    {
        var slash = relativePath.IndexOf('/');
        var folder = slash < 0 ? relativePath : relativePath.Substring(0, slash);
        if (folder.Equals("fo4", StringComparison.OrdinalIgnoreCase)
            || folder.Equals("fallout4", StringComparison.OrdinalIgnoreCase))
        {
            return PapyrusDialect.Fallout4;
        }

        if (folder.Equals("starfield", StringComparison.OrdinalIgnoreCase)
            || folder.Equals("sf", StringComparison.OrdinalIgnoreCase))
        {
            return PapyrusDialect.Starfield;
        }

        return PapyrusDialect.SkyrimSe;
    }
}
