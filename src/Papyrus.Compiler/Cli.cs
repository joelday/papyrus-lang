using System;
using System.IO;

namespace Papyrus.Compiler;

internal static class Cli
{
    internal const string HelpText =
        """
        Usage: papyrusc corpus acquire --game <game> --source <path> [--dest <path>] [--force]

          --game     skyrimse | fallout4 | starfield
          --source   Local game install or scripts directory (never downloaded)
          --dest     Destination directory (default: ./corpus/<game>)
          --force    Allow destination inside a git worktree

        Copies .psc and .pex files only. Does not contact the network.
        The corpus is user-owned and gitignored. See docs/corpus.md.
        """;

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || WantsHelp(args))
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        if (args[0] == "corpus" && args.Length == 1)
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        if (args.Length >= 2 && args[0] == "corpus" && args[1] == "acquire")
        {
            if (!TryParseAcquire(args.AsSpan(2), out var options, out var error))
            {
                stderr.WriteLine(error);
                return 1;
            }

            return CorpusAcquire.Execute(options, stdout, stderr);
        }

        stderr.WriteLine("error: unknown command");
        stdout.WriteLine(HelpText);
        return 1;
    }

    private static bool WantsHelp(string[] args)
    {
        if (args[0] is "help" or "-h" or "--help")
        {
            return true;
        }

        foreach (var arg in args)
        {
            if (arg is "-h" or "--help")
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryParseAcquire(
        ReadOnlySpan<string> args,
        out CorpusAcquireOptions options,
        out string error)
    {
        options = null!;
        error = null!;
        string? game = null;
        string? source = null;
        string? dest = null;
        var force = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length < 3)
            {
                error = $"error: unexpected argument '{arg}'";
                return false;
            }

            var eq = arg.IndexOf('=');
            string name;
            string? value;
            if (eq >= 0)
            {
                name = arg[2..eq];
                value = arg[(eq + 1)..];
            }
            else
            {
                name = arg[2..];
                value = null;
            }

            if (name == "force")
            {
                force = true;
                continue;
            }

            if (value is null)
            {
                if (i + 1 >= args.Length)
                {
                    error = $"error: --{name} requires a value";
                    return false;
                }

                value = args[++i];
            }

            switch (name)
            {
                case "game":
                    game = value;
                    break;
                case "source":
                    source = value;
                    break;
                case "dest":
                    dest = value;
                    break;
                default:
                    error = $"error: unknown option --{name}";
                    return false;
            }
        }

        if (game is null || !CorpusAcquireOptions.TryCanonicalGame(game, out var canonical))
        {
            error = "error: --game must be one of: skyrimse, fallout4, starfield";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "error: --source is required";
            return false;
        }

        options = new CorpusAcquireOptions(canonical, source, dest, force);
        return true;
    }
}

internal sealed class CorpusAcquireOptions
{
    internal CorpusAcquireOptions(string game, string source, string? dest, bool force)
    {
        Game = game;
        Source = source;
        Dest = dest;
        Force = force;
    }

    internal string Game { get; }
    internal string Source { get; }
    internal string? Dest { get; }
    internal bool Force { get; }

    internal static bool TryCanonicalGame(string value, out string game)
    {
        foreach (var candidate in new[] { "skyrimse", "fallout4", "starfield" })
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                game = candidate;
                return true;
            }
        }

        game = null!;
        return false;
    }
}
