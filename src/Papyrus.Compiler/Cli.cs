using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Papyrus.Compiler;

internal static class Cli
{
    internal const string HelpText =
        """
        Usage: papyrusc <command>

          papyrusc compile <script.psc|project.ppj> [--game <skyrimse|skyrimle|fallout4|starfield>] [--output <file.pex>] [--flags <file.flg>] [--import <dir>]... [--time <unix>] [--ignore-engine-limits] [--champollion-compat] [--pyro <pyro.exe>]
          papyrusc --version

        compile
          Compile one .psc to .pex, or every script in a .ppj project.
          --game     skyrimse | skyrimle | fallout4 | starfield
                     May be omitted when the .ppj specifies Game.
          --output   Output .pex path, or output directory for .ppj
          --flags    User flags file (.flg)
          --import   Additional script import directory (repeatable)
          --time     Unix compile timestamp (default: 0)
          --ignore-engine-limits  Downgrade engine-limit diagnostics to warnings
          --champollion-compat    Accept ::-prefixed identifiers and Script#Struct types
          --pyro     Pyro executable whose download cache and token serve a .ppj's
                     remote imports (default: PAPYRUS_PYRO, then the PATH)
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

        if (args[0] == "compile")
        {
            if (!TryParseCompile(args.AsSpan(1), out var options, out var error))
            {
                stderr.WriteLine(error);
                return 1;
            }

            return CompileCommand.Execute(options, stdout, stderr);
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

    internal static bool TryParseCompile(
        ReadOnlySpan<string> args,
        out CompileCommandOptions options,
        out string error)
    {
        options = null!;
        error = null!;
        string? path = null;
        string? game = null;
        string? output = null;
        string? flags = null;
        string? pyro = null;
        var imports = new List<string>();
        long time = 0;
        var ignoreEngineLimits = false;
        var champollionCompat = false;

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

            if (arg.Length < 3)
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

            if (name == "ignore-engine-limits")
            {
                ignoreEngineLimits = true;
                continue;
            }

            if (name == "champollion-compat")
            {
                champollionCompat = true;
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
                case "output":
                    output = value;
                    break;
                case "flags":
                    flags = value;
                    break;
                case "import":
                    imports.Add(value);
                    break;
                case "pyro":
                    pyro = value;
                    break;
                case "time":
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out time) || time < 0)
                    {
                        error = "error: --time must be a non-negative unix timestamp";
                        return false;
                    }

                    break;
                default:
                    error = $"error: unknown option --{name}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "error: path to a .psc or .ppj file is required";
            return false;
        }

        var isPpj = Path.GetExtension(path).Equals(".ppj", StringComparison.OrdinalIgnoreCase);
        if (game is not null && !CompileCommand.TryGetDialect(game, out _))
        {
            error = "error: --game must be one of: " + CompileCommand.GameValues;
            return false;
        }

        if (!isPpj && (game is null || !CompileCommand.TryGetDialect(game, out _)))
        {
            error = "error: --game must be one of: " + CompileCommand.GameValues;
            return false;
        }

        options = new CompileCommandOptions(path, game, output, flags, imports.ToArray(), time, ignoreEngineLimits, champollionCompat, pyro);
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
