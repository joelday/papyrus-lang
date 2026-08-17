using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Testing;

/// <summary>
/// Normalized SHA-256 of an existing <c>.pex</c>. Does not compile.
/// </summary>
public sealed class PexHashEntry
{
    public PexHashEntry(string relativePath, string scriptName, string sha256)
    {
        RelativePath = relativePath;
        ScriptName = scriptName;
        Sha256 = sha256;
    }

    public string RelativePath { get; }
    public string ScriptName { get; }
    public string Sha256 { get; }
}

/// <summary>
/// Hashes existing PEX via <see cref="PexReader"/> + <see cref="PexNormalizer.Hash"/>.
/// Public CI uses synthetic fixtures only; vanilla corpus is never committed.
/// </summary>
public static class PexHashHarness
{
    public static string HashBytes(ReadOnlySpan<byte> pex, GameId? preferredGame = null)
    {
        return PexNormalizer.Hash(PexReader.Read(pex, preferredGame));
    }

    public static string HashFile(string path, GameId? preferredGame = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = File.ReadAllBytes(path);
        return HashBytes(bytes, preferredGame);
    }

    public static ImmutableArray<PexHashEntry> HashDirectory(string path, GameId? preferredGame = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"source not found: {full}");

        var files = ListPex(full);
        var builder = ImmutableArray.CreateBuilder<PexHashEntry>(files.Count);
        foreach (string file in files)
        {
            string relative = NormalizeRelative(Path.GetRelativePath(full, file));
            builder.Add(new PexHashEntry(relative, ScriptName(relative), HashFile(file, preferredGame)));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Hashes a file or every <c>.pex</c> under a directory.
    /// </summary>
    public static ImmutableArray<PexHashEntry> HashPath(string path, GameId? preferredGame = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (File.Exists(full))
        {
            if (!IsPex(full))
                throw new InvalidOperationException($"not a .pex file: {full}");

            string name = Path.GetFileName(full);
            return [new PexHashEntry(name, ScriptName(name), HashFile(full, preferredGame))];
        }

        return HashDirectory(full, preferredGame);
    }

    internal static string ScriptName(string relativePath)
    {
        string name = relativePath.Replace('\\', '/');
        if (name.EndsWith(".pex", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }

    internal static string NormalizeRelative(string relative) => relative.Replace('\\', '/');

    internal static bool IsPex(string path) =>
        path.EndsWith(".pex", StringComparison.OrdinalIgnoreCase);

    internal static List<string> ListPex(string source)
    {
        var files = new List<string>();
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (!IsPex(file))
                continue;

            string relative = Path.GetRelativePath(source, file);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
                continue;

            files.Add(file);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }
}
