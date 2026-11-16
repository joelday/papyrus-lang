using System;
using System.IO;
using Papyrus.CodeAnalysis.Testing;

namespace Papyrus.Compliance;

internal static class Cli
{
    internal const string HelpText =
        """
        Usage: papyrus-compliance <command>

          papyrus-compliance scripts acquire --game <game> --source <path> [--dest <path>] [--force]
          papyrus-compliance hash <path> [--game <game>]
          papyrus-compliance verify --source <path> --baseline <file> [--game <game>]
          papyrus-compliance verify --compile [--game <game>] [--source <path>] [--baseline <file>] [--allowlist <file>] [--flags <file.flg>]
          papyrus-compliance verify --write-baselines --source <path> --out <file> --attestation <file> --game <game>
          papyrus-compliance verify --champollion [--source <path>] [--game <game>] [--champollion-path <bin>]
          papyrus-compliance --version

        Checks papyrusc output against the Creation Kit compiler. Development tooling;
        not shipped with papyrusc.

        scripts acquire
          --game     skyrimse | fallout4 | starfield
          --source   Local game install or scripts directory
          --dest     Destination directory (default: ./game-scripts/<game>)
          --force    Allow destination inside a git worktree
          Copies .psc and .pex files only.

        hash
          Normalized SHA-256 of existing .pex via PexReader + PexNormalizer.

        verify
          Compare hashes of existing .pex to a baseline. Does not compile
          unless --compile is set.
          --baseline is required unless --write-baselines, --champollion, or --compile with no game scripts.
          --compile   Compile each .psc with PexEmitter and compare normalized hashes
                      to the baseline. Skips (exit 0) when the local game scripts are absent.
                      --source defaults to ./game-scripts/<game>.
          --allowlist Known differences from the CK compiler (default allowlist.md next to this tool).
          --flags     User flags file (.flg). Defaults to TESV_Papyrus_Flags.flg /
                      Institute_Papyrus_Flags.flg in the game scripts if present.
          --ck-compiler <path>  Compile with the CK compiler too; not implemented yet.
          --write-baselines requires --attestation and --game.

        verify --champollion
          Decompile vanilla .pex with Champollion, recompile with --champollion-compat,
          and compare normalized hashes.
          Skips (exit 0) when the Champollion binary or game scripts are missing.
          --source             Directory of .pex (default: ./game-scripts/<game> or ./game-scripts)
          --champollion-path   Champollion executable (else PAPYRUSC_CHAMPOLLION, PATH, tools/Champollion)
        """;

    // GetName().Version is AOT-safe; GetCustomAttribute is annotated RequiresUnreferencedCode.
    internal static string Version =>
        typeof(Cli).Assembly.GetName().Version?.ToString() ?? "0.0.0";

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

        if (args[0] == "scripts" && args.Length == 1)
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        if (args.Length >= 2 && args[0] == "scripts" && args[1] == "acquire")
        {
            if (!TryParseAcquire(args.AsSpan(2), out var options, out var error))
            {
                stderr.WriteLine(error);
                return 1;
            }

            return GameScriptsAcquire.Execute(options, stdout, stderr);
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
        out GameScriptsAcquireOptions options,
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

        if (game is null || !GameScriptsAcquireOptions.TryCanonicalGame(game, out var canonical))
        {
            error = "error: --game must be one of: skyrimse, fallout4, starfield";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "error: --source is required";
            return false;
        }

        options = new GameScriptsAcquireOptions(canonical, source, dest, force);
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
        var compile = false;
        string? allowlist = null;
        string? ckCompiler = null;
        string? flags = null;
        var champollion = false;
        string? champollionPath = null;

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

            if (name == "compile")
            {
                compile = true;
                continue;
            }

            if (name == "champollion")
            {
                champollion = true;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    champollionPath = value;
                }

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
                case "allowlist":
                    allowlist = value;
                    break;
                case "ck-compiler":
                    ckCompiler = value;
                    break;
                case "flags":
                    flags = value;
                    break;
                case "champollion-path":
                    champollionPath = value;
                    break;
                default:
                    error = $"error: unknown option --{name}";
                    return false;
            }
        }

        if (champollion && writeBaselines)
        {
            error = "error: --champollion cannot be used with --write-baselines";
            return false;
        }

        if (champollion && compile)
        {
            error = "error: --champollion cannot be combined with --compile";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ckCompiler))
        {
            error = "error: --ck-compiler (tier-2) is not implemented; use --compile for compile-vs-baseline";
            return false;
        }

        if (compile && writeBaselines)
        {
            error = "error: --compile cannot be combined with --write-baselines";
            return false;
        }

        if (!compile && !champollion && string.IsNullOrWhiteSpace(source))
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
                error = "error: --write-baselines requires --attestation <file>";
                return false;
            }

            if (string.IsNullOrWhiteSpace(game))
            {
                error = "error: --game is required with --write-baselines";
                return false;
            }
        }
        else if (!compile && !champollion && string.IsNullOrWhiteSpace(baseline))
        {
            error = "error: --baseline is required (use papyrus-compliance hash to print hashes)";
            return false;
        }

        options = new VerifyCommandOptions(
            source ?? "",
            game,
            baseline,
            dest,
            attestation,
            writeBaselines,
            compile,
            allowlist,
            ckCompiler,
            flags,
            champollion,
            champollionPath);
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

internal sealed class GameScriptsAcquireOptions
{
    internal GameScriptsAcquireOptions(string game, string source, string? dest, bool force)
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
