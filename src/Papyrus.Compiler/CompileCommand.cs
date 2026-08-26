using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Papyrus.Workspaces;

namespace Papyrus.Compiler;

internal sealed class CompileCommandOptions
{
    internal CompileCommandOptions(
        string path,
        string game,
        string? output,
        string? flags,
        string[] imports,
        long time,
        bool champollionCompat)
    {
        Path = path;
        Game = game;
        Output = output;
        Flags = flags;
        Imports = imports;
        Time = time;
        ChampollionCompat = champollionCompat;
    }

    internal string Path { get; }
    internal string Game { get; }
    internal string? Output { get; }
    internal string? Flags { get; }
    internal string[] Imports { get; }
    internal long Time { get; }
    internal bool ChampollionCompat { get; }
}

internal static class CompileCommand
{
    internal const string GameValues = "skyrimse, skyrimle, fallout4, starfield";

    internal static int Execute(CompileCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (!TryGetDialect(options.Game, out var dialect))
        {
            stderr.WriteLine("error: --game must be one of: " + GameValues);
            return 1;
        }

        if (!TryGetFullPath(options.Path, stderr, out var scriptPath))
        {
            return 1;
        }

        if (!File.Exists(scriptPath))
        {
            stderr.WriteLine($"error: path not found: {scriptPath}");
            return 1;
        }

        var extension = Path.GetExtension(scriptPath);
        if (extension.Equals(".ppj", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("error: .ppj project builds are not supported");
            return 1;
        }

        if (!extension.Equals(".psc", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("error: expected a .psc file");
            return 1;
        }

        if (!TryGetFullPath(options.Output ?? Path.ChangeExtension(scriptPath, ".pex"), stderr, out var outputPath))
        {
            return 1;
        }

        FlagsFile? flagsFile = null;
        string? flagsPath = null;
        if (options.Flags is not null)
        {
            if (!TryGetFullPath(options.Flags, stderr, out flagsPath))
            {
                return 1;
            }

            if (!File.Exists(flagsPath))
            {
                stderr.WriteLine($"error: flags file not found: {flagsPath}");
                return 1;
            }
        }

        var imports = new List<string>(options.Imports.Length);
        foreach (var import in options.Imports)
        {
            if (string.IsNullOrWhiteSpace(import))
            {
                continue;
            }

            if (!TryGetFullPath(import, stderr, out var importPath))
            {
                return 1;
            }

            imports.Add(importPath);
        }

        var includeDirectories = new List<string>(1 + imports.Count);
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDirectory))
        {
            includeDirectories.Add(scriptDirectory);
        }

        includeDirectories.AddRange(imports);

        FileSystemScriptResolver resolver;
        try
        {
            resolver = new FileSystemScriptResolver(
                includeDirectories,
                Path.GetDirectoryName(outputPath),
                dialect.Game);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        var name = GetScriptName(scriptPath, imports, dialect.Game);
        if (name.Parts.Length == 0)
        {
            stderr.WriteLine("error: could not determine script name");
            return 1;
        }

        try
        {
            if (flagsPath is not null)
            {
                flagsFile = FlagsFile.Parse(File.ReadAllText(flagsPath));
            }

            var text = SourceText.From(File.ReadAllBytes(scriptPath));
            var parseOptions = new PapyrusParseOptions { ChampollionCompat = options.ChampollionCompat };
            var compilation = PapyrusCompilation.Create(dialect, resolver, parseOptions)
                .WithScriptText(name, text);
            var script = compilation.GetScript(name);
            var emitter = new PexEmitter(flagsFile);
            var pex = emitter.EmitFile(script);
            pex.CompileTime = DateTimeOffset.FromUnixTimeSeconds(options.Time);

            var hasError = false;
            if (flagsFile is not null && flagsPath is not null)
            {
                hasError |= WriteFlagsDiagnostics(flagsFile, flagsPath, stderr);
            }

            hasError |= WriteDiagnostics(compilation.GetDiagnostics(), scriptPath, name, compilation, stderr);
            hasError |= WriteDiagnostics(emitter.Diagnostics, scriptPath, name, compilation, stderr);
            if (hasError)
            {
                return 1;
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllBytes(outputPath, PexWriter.Write(pex));
            stdout.WriteLine(outputPath);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or InvalidOperationException or PexFormatException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static bool TryGetFullPath(string path, TextWriter stderr, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            fullPath = null!;
            return false;
        }
    }

    internal static bool TryGetDialect(string game, out PapyrusDialect dialect)
    {
        dialect = null!;
        if (string.Equals(game, "skyrimse", StringComparison.OrdinalIgnoreCase))
        {
            dialect = PapyrusDialect.SkyrimSe;
            return true;
        }

        if (string.Equals(game, "skyrimle", StringComparison.OrdinalIgnoreCase))
        {
            dialect = PapyrusDialect.SkyrimLe;
            return true;
        }

        if (string.Equals(game, "fallout4", StringComparison.OrdinalIgnoreCase))
        {
            dialect = PapyrusDialect.Fallout4;
            return true;
        }

        if (string.Equals(game, "starfield", StringComparison.OrdinalIgnoreCase))
        {
            dialect = PapyrusDialect.Starfield;
            return true;
        }

        return false;
    }

    internal static bool TryGetDialect(GameId game, out PapyrusDialect dialect)
    {
        dialect = null!;
        switch (game)
        {
            case GameId.Skyrim:
                dialect = PapyrusDialect.SkyrimLe;
                return true;
            case GameId.SkyrimSpecialEdition:
                dialect = PapyrusDialect.SkyrimSe;
                return true;
            case GameId.Fallout4:
                dialect = PapyrusDialect.Fallout4;
                return true;
            case GameId.Starfield:
                dialect = PapyrusDialect.Starfield;
                return true;
            default:
                return false;
        }
    }

    internal static QualifiedName GetScriptName(string fullPath, List<string> imports, GameId game)
    {
        var supportsNamespaces = game is not (GameId.Skyrim or GameId.SkyrimSpecialEdition);
        string? bestImport = null;
        for (var i = 0; i < imports.Count; i++)
        {
            var import = imports[i];
            if (!IsUnderDirectory(import, fullPath))
            {
                continue;
            }

            if (bestImport is null || import.Length > bestImport.Length)
            {
                bestImport = import;
            }
        }

        if (bestImport is not null && TryGetRelativeName(bestImport, fullPath, out var name))
        {
            if (supportsNamespaces || name.Parts.Length == 1)
            {
                return name;
            }
        }

        return QualifiedName.Parse(Path.GetFileNameWithoutExtension(fullPath));
    }

    private static bool IsUnderDirectory(string directory, string fullPath)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.Length <= prefix.Length)
        {
            return false;
        }

        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = fullPath[prefix.Length];
        return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
    }

    private static bool TryGetRelativeName(string include, string fullPath, out QualifiedName name)
    {
        name = default;
        var prefix = include.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.Length <= prefix.Length)
        {
            return false;
        }

        var relative = fullPath[(prefix.Length + 1)..].Replace('\\', '/');
        if (relative.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^4];
        }

        if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        name = QualifiedName.Parse(relative.Replace('/', ':'));
        return name.Parts.Length > 0;
    }

    private static bool WriteFlagsDiagnostics(FlagsFile flagsFile, string flagsPath, TextWriter stderr)
    {
        var hasError = false;
        foreach (var info in flagsFile.Diagnostics)
        {
            var severity = info.Descriptor.DefaultSeverity;
            if (severity == DiagnosticSeverity.Hidden)
            {
                continue;
            }

            if (severity == DiagnosticSeverity.Error)
            {
                hasError = true;
            }

            stderr.WriteLine($"{flagsPath}: {SeverityText(severity)} {info.Id}: {info.GetMessage()}");
        }

        return hasError;
    }

    private static bool WriteDiagnostics(
        ImmutableArray<Diagnostic> diagnostics,
        string scriptPath,
        QualifiedName name,
        PapyrusCompilation compilation,
        TextWriter stderr)
    {
        if (diagnostics.IsDefaultOrEmpty)
        {
            return false;
        }

        var hasError = false;
        var mainTree = compilation.GetSyntaxTree(name);
        var paths = new Dictionary<SyntaxTree, string?>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Hidden)
            {
                continue;
            }

            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                hasError = true;
            }

            var path = GetDisplayPath(diagnostic.Tree, mainTree, scriptPath, compilation, paths);
            var prefix = SeverityText(diagnostic.Severity) + " " + diagnostic.Id + ": " + diagnostic.GetMessage();
            if (path is null)
            {
                stderr.WriteLine(prefix);
            }
            else if (diagnostic.Tree is null)
            {
                stderr.WriteLine($"{path}: {prefix}");
            }
            else
            {
                GetLineAndColumn(diagnostic.Tree.GetText(), diagnostic.Span.Start, out var line, out var column);
                stderr.WriteLine($"{path}({line},{column}): {prefix}");
            }
        }

        return hasError;
    }

    private static string? GetDisplayPath(
        SyntaxTree? tree,
        SyntaxTree mainTree,
        string scriptPath,
        PapyrusCompilation compilation,
        Dictionary<SyntaxTree, string?> cache)
    {
        if (tree is null || ReferenceEquals(tree, mainTree))
        {
            return scriptPath;
        }

        if (cache.TryGetValue(tree, out var cached))
        {
            return cached;
        }

        string? path = null;
        if (compilation.GetSemanticModel(tree).GetDeclaredSymbol(tree.GetRoot()) is IScriptSymbol script)
        {
            if (compilation.Resolver.TryResolve(script.QualifiedName, out _, out var displayPath)
                && !string.IsNullOrEmpty(displayPath))
            {
                path = displayPath;
            }
            else
            {
                path = script.QualifiedName.ToRelativePath();
            }
        }

        cache[tree] = path;
        return path;
    }

    private static void GetLineAndColumn(SourceText text, int position, out int line, out int column)
    {
        line = 1;
        column = 1;
        if (position < 0)
        {
            return;
        }

        var end = position < text.Length ? position : text.Length;
        for (var i = 0; i < end; i++)
        {
            if (text.GetChar(i) == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
    }

    private static string SeverityText(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            DiagnosticSeverity.Hidden => "hidden",
            _ => "error",
        };
    }
}
