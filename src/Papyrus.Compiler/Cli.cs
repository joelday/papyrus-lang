using System;
using System.IO;
using System.Reflection;
using Papyrus.CodeAnalysis.Testing;

namespace Papyrus.Compiler;

internal static class Cli
{
    internal const string HelpText =
        """
        Usage: papyrusc <command>

          papyrusc corpus acquire --game <game> --source <path> [--dest <path>] [--force]
          papyrusc hash <path> [--game <game>]
          papyrusc verify --source <path> --baseline <file> [--game <game>]
          papyrusc verify --write-baselines --source <path> --out <file> --attestation <file> --game <game>
          papyrusc --version

        corpus acquire
          --game     skyrimse | fallout4 | starfield
          --source   Local game install or scripts directory (never downloaded)
          --dest     Destination directory (default: ./corpus/<game>)
          --force    Allow destination inside a git worktree
          Copies .psc and .pex files only. Does not contact the network.
          The corpus is user-owned and gitignored. See docs/corpus.md.

        hash
          Normalized SHA-256 of existing .pex via PexReader + PexNormalizer.
          Does not compile. Does not download Bethesda files.

        verify
          Compare hashes of existing .pex to a committed baseline. Does not compile.
          --baseline is required unless --write-baselines.
          --write-baselines is maintainer-only and requires --attestation and --game.
          See docs/compliance/baselines/README.md.
        """;

    internal static string Version =>
        typeof(Cli).Assembly.GetName().Version?.ToString()
        ?? typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || WantsHelp(args))
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        if (WantsVersion(args))
        {
            stdout.WriteLine(Version);
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

        if (args[0] == "hash")
        {
            if (!TryParseHash(args.AsSpan(1), out var options, out var error))
            {
                stderr.WriteLine(error);
                return 1;
            }

            return HashCommand.Execute(options, stdout, stderr);
        }

        if (args[0] is "verify" or "baseline")
        {
            if (!TryParseVerify(args.AsSpan(1), out var options, out var error))
            {
                stderr.WriteLine(error);
                return 1;
            }

            return VerifyCommand.Execute(options, stdout, stderr);
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

    private static bool WantsVersion(string[] args)
    {
        if (args[0] is "version" or "--version" or "-V")
            return true;

        foreach (var arg in args)
        {
            if (arg is "--version" or "-V")
                return true;
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

    internal static bool TryParseHash(
        ReadOnlySpan<string> args,
        out HashCommandOptions options,
        out string error)
    {
        options = null!;
        error = null!;
        string? path = null;
        string? game = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (path is not null)
                {
                    error = $"error: unexpected argument '{arg}'";
                    return false;
                }

                path = arg;
                continue;
            }

            if (!TryReadOption(args, ref i, out var name, out var value, out error))
                return false;

            switch (name)
            {
                case "game":
                    game = value;
                    break;
                default:
                    error = $"error: unknown option --{name}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "error: path to a .pex file or directory is required";
            return false;
        }

        if (game is not null && !GameNames.TryParse(game, out _, out _))
        {
            error = "error: --game must be one of: skyrimse, fallout4, starfield, skyrimle";
            return false;
        }

        options = new HashCommandOptions(path, game);
        return true;
    }

    internal static bool TryParseVerify(
        ReadOnlySpan<string> args,
        out VerifyCommandOptions options,
        out string error)
    {
        options = null!;
        error = null!;
        string? source = null;
        string? game = null;
        string? baseline = null;
        string? dest = null;
        string? attestation = null;
        var writeBaselines = false;

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

            if (name == "write-baselines")
            {
                writeBaselines = true;
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
                case "source":
                    source = value;
                    break;
                case "game":
                    game = value;
                    break;
                case "baseline":
                    baseline = value;
                    break;
                case "out":
                    dest = value;
                    break;
                case "attestation":
                    attestation = value;
                    break;
                default:
                    error = $"error: unknown option --{name}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "error: --source is required";
            return false;
        }

        if (game is not null && !GameNames.TryParse(game, out _, out _))
        {
            error = "error: --game must be one of: skyrimse, fallout4, starfield, skyrimle";
            return false;
        }

        if (writeBaselines)
        {
            if (string.IsNullOrWhiteSpace(dest))
            {
                error = "error: --out is required with --write-baselines";
                return false;
            }

            if (string.IsNullOrWhiteSpace(attestation))
            {
                error = "error: --write-baselines requires --attestation <file> (see docs/compliance/baselines/README.md)";
                return false;
            }

            if (string.IsNullOrWhiteSpace(game))
            {
                error = "error: --game is required with --write-baselines";
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(baseline))
        {
            error = "error: --baseline is required (use papyrusc hash to print hashes)";
            return false;
        }

        options = new VerifyCommandOptions(source, game, baseline, dest, attestation, writeBaselines);
        return true;
    }

    private static bool TryReadOption(
        ReadOnlySpan<string> args,
        ref int i,
        out string name,
        out string value,
        out string error)
    {
        name = null!;
        value = null!;
        error = null!;
        var arg = args[i];
        if (arg.Length < 3)
        {
            error = $"error: unexpected argument '{arg}'";
            return false;
        }

        var eq = arg.IndexOf('=');
        string? parsedValue;
        if (eq >= 0)
        {
            name = arg[2..eq];
            parsedValue = arg[(eq + 1)..];
        }
        else
        {
            name = arg[2..];
            parsedValue = null;
        }

        if (parsedValue is null)
        {
            if (i + 1 >= args.Length)
            {
                error = $"error: --{name} requires a value";
                return false;
            }

            parsedValue = args[++i];
        }

        value = parsedValue;
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
