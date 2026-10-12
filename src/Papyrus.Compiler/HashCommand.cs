using System;
using System.IO;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.Pex;

namespace Papyrus.Compiler;

internal sealed class HashCommandOptions
{
    internal HashCommandOptions(string path, string? game)
    {
        Path = path;
        Game = game;
    }

    internal string Path { get; }
    internal string? Game { get; }
}

internal static class HashCommand
{
    internal static int Execute(HashCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
        GameId? preferred = null;
        if (options.Game is not null)
        {
            if (!GameNames.TryParse(options.Game, out _, out var game))
            {
                stderr.WriteLine("error: --game must be one of: skyrimse, fallout4, starfield, skyrimle");
                return 1;
            }

            preferred = game;
        }

        string path;
        try
        {
            path = System.IO.Path.GetFullPath(options.Path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            stderr.WriteLine($"error: path not found: {path}");
            return 1;
        }

        try
        {
            var entries = PexHashHarness.HashPath(path, preferred);
            foreach (var entry in entries)
                stdout.WriteLine($"{entry.Sha256}  {entry.RelativePath}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PexFormatException or InvalidOperationException or DirectoryNotFoundException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
