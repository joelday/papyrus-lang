using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Xml;
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
        string? game,
        string? output,
        string? flags,
        string[] imports,
        long time,
        bool ignoreEngineLimits)
    {
        Path = path;
        Game = game;
        Output = output;
        Flags = flags;
        Imports = imports;
        Time = time;
        IgnoreEngineLimits = ignoreEngineLimits;
    }

    internal string Path { get; }
    internal string? Game { get; }
    internal string? Output { get; }
    internal string? Flags { get; }
    internal string[] Imports { get; }
    internal long Time { get; }
    internal bool IgnoreEngineLimits { get; }
}

internal static class CompileCommand
{
    internal const string GameValues = "skyrimse, skyrimle, fallout4, starfield";

    internal static int Execute(CompileCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (options.Game is not null && !TryGetDialect(options.Game, out _))
        {
            stderr.WriteLine("error: --game must be one of: " + GameValues);
            return 1;
        }

        if (!TryGetFullPath(options.Path, stderr, out var inputPath))
        {
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            stderr.WriteLine($"error: path not found: {inputPath}");
            return 1;
        }

        var extension = Path.GetExtension(inputPath);
        if (extension.Equals(".ppj", StringComparison.OrdinalIgnoreCase))
        {
            return CompileProject(options, inputPath, stdout, stderr);
        }

        if (!extension.Equals(".psc", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("error: expected a .psc or .ppj file");
            return 1;
        }

        if (options.Game is null || !TryGetDialect(options.Game, out var dialect))
        {
            stderr.WriteLine("error: --game must be one of: " + GameValues);
            return 1;
        }

        return CompileSingleScript(options, inputPath, dialect, stdout, stderr);
    }

    private static int CompileSingleScript(
        CompileCommandOptions options,
        string scriptPath,
        PapyrusDialect dialect,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!TryGetFullPath(options.Output ?? Path.ChangeExtension(scriptPath, ".pex"), stderr, out var outputPath))
        {
            return 1;
        }

        if (!TryResolveCliImports(options.Imports, stderr, out var imports))
        {
            return 1;
        }

        if (!TryLoadFlags(options.Flags, projectDirectory: null, includeDirectories: null, stderr, out var flagsFile, out var flagsPath))
        {
            return 1;
        }

        var includeDirectories = new List<string>(1 + imports.Count);
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDirectory))
        {
            includeDirectories.Add(scriptDirectory);
        }

        includeDirectories.AddRange(imports);

        if (!TryCreateResolver(includeDirectories, Path.GetDirectoryName(outputPath), dialect.Game, stderr, out var resolver))
        {
            return 1;
        }

        return EmitScript(
            scriptPath,
            outputPath,
            dialect,
            resolver,
            imports,
            flagsFile,
            flagsPath,
            writeFlagsDiagnostics: true,
            options.Time,
            options.IgnoreEngineLimits,
            stdout,
            stderr);
    }

    private static int CompileProject(
        CompileCommandOptions options,
        string projectPath,
        TextWriter stdout,
        TextWriter stderr)
    {
        PapyrusProject project;
        try
        {
            using var stream = File.OpenRead(projectPath);
            project = PpjParser.Parse(stream).WithPath(projectPath);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or IOException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        PapyrusDialect dialect;
        if (options.Game is not null)
        {
            if (!TryGetDialect(options.Game, out dialect))
            {
                stderr.WriteLine("error: --game must be one of: " + GameValues);
                return 1;
            }
        }
        else if (project.Dialect is null)
        {
            stderr.WriteLine("error: --game must be one of: " + GameValues);
            return 1;
        }
        else
        {
            dialect = project.Dialect;
        }

        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDirectory))
        {
            stderr.WriteLine("error: project path has no directory");
            return 1;
        }

        if (!TryResolveCliImports(options.Imports, stderr, out var cliImports))
        {
            return 1;
        }

        var includeDirectories = new List<string>();
        foreach (var import in project.Imports)
        {
            if (string.IsNullOrWhiteSpace(import))
            {
                continue;
            }

            if (!TryResolveProjectPath(projectDirectory, import, stderr, out var importPath))
            {
                return 1;
            }

            includeDirectories.Add(importPath);
        }

        foreach (var folder in project.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Path))
            {
                continue;
            }

            if (!TryResolveProjectPath(projectDirectory, folder.Path, stderr, out var folderPath))
            {
                return 1;
            }

            includeDirectories.Add(folderPath);
        }

        includeDirectories.Add(projectDirectory);
        includeDirectories.AddRange(cliImports);

        string? outputDirectory = null;
        var outputSpec = options.Output ?? project.Output;
        if (outputSpec is not null)
        {
            if (!TryResolveProjectPath(projectDirectory, outputSpec, stderr, out outputDirectory))
            {
                return 1;
            }
        }

        if (!TryLoadFlags(
                options.Flags ?? project.Flags,
                options.Flags is null ? projectDirectory : null,
                options.Flags is null ? includeDirectories : null,
                stderr,
                out var flagsFile,
                out var flagsPath))
        {
            return 1;
        }

        if (flagsFile is not null && flagsPath is not null && WriteFlagsDiagnostics(flagsFile, flagsPath, stderr))
        {
            return 1;
        }

        if (!TryCollectProjectScripts(project, projectDirectory, stderr, out var scripts))
        {
            return 1;
        }

        if (!TryCreateResolver(includeDirectories, outputDirectory, dialect.Game, stderr, out var resolver))
        {
            return 1;
        }

        var failed = false;
        foreach (var scriptPath in scripts)
        {
            if (!TryGetScriptOutputPath(scriptPath, outputDirectory, includeDirectories, dialect.Game, stderr, out var outputPath))
            {
                failed = true;
                continue;
            }

            var code = EmitScript(
                scriptPath,
                outputPath,
                dialect,
                resolver,
                includeDirectories,
                flagsFile,
                flagsPath,
                writeFlagsDiagnostics: false,
                options.Time,
                options.IgnoreEngineLimits,
                stdout,
                stderr);
            if (code != 0)
            {
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static int EmitScript(
        string scriptPath,
        string outputPath,
        PapyrusDialect dialect,
        FileSystemScriptResolver resolver,
        List<string> nameDirectories,
        FlagsFile? flagsFile,
        string? flagsPath,
        bool writeFlagsDiagnostics,
        long time,
        bool ignoreEngineLimits,
        TextWriter stdout,
        TextWriter stderr)
    {
        var name = GetScriptName(scriptPath, nameDirectories, dialect.Game);
        if (name.Parts.Length == 0)
        {
            stderr.WriteLine("error: could not determine script name");
            return 1;
        }

        try
        {
            var text = SourceText.From(File.ReadAllBytes(scriptPath));
            var parseOptions = ignoreEngineLimits
                ? new PapyrusParseOptions { IgnoreEngineLimits = true }
                : PapyrusParseOptions.Default;
            var compilation = PapyrusCompilation.Create(dialect, resolver, parseOptions)
                .WithScriptText(name, text);
            var script = compilation.GetScript(name);
            var emitter = new PexEmitter(flagsFile);
            var pex = emitter.EmitFile(script);
            pex.CompileTime = DateTimeOffset.FromUnixTimeSeconds(time);

            var hasError = false;
            if (writeFlagsDiagnostics && flagsFile is not null && flagsPath is not null)
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

    private static bool TryResolveCliImports(string[] imports, TextWriter stderr, out List<string> resolved)
    {
        resolved = new List<string>(imports.Length);
        foreach (var import in imports)
        {
            if (string.IsNullOrWhiteSpace(import))
            {
                continue;
            }

            if (!TryGetFullPath(import, stderr, out var importPath))
            {
                return false;
            }

            resolved.Add(importPath);
        }

        return true;
    }

    private static bool TryCreateResolver(
        List<string> includeDirectories,
        string? outputDirectory,
        GameId game,
        TextWriter stderr,
        out FileSystemScriptResolver resolver)
    {
        try
        {
            resolver = new FileSystemScriptResolver(includeDirectories, outputDirectory, game);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            resolver = null!;
            return false;
        }
    }

    private static bool TryLoadFlags(
        string? flags,
        string? projectDirectory,
        List<string>? includeDirectories,
        TextWriter stderr,
        out FlagsFile? flagsFile,
        out string? flagsPath)
    {
        flagsFile = null;
        flagsPath = null;
        if (flags is null)
        {
            return true;
        }

        if (projectDirectory is not null)
        {
            if (!TryResolveProjectPath(projectDirectory, flags, stderr, out var candidate))
            {
                return false;
            }

            if (File.Exists(candidate))
            {
                flagsPath = candidate;
            }
            else if (includeDirectories is not null)
            {
                var fileName = Path.GetFileName(flags);
                if (!string.IsNullOrEmpty(fileName))
                {
                    for (var i = 0; i < includeDirectories.Count; i++)
                    {
                        var search = Path.Combine(includeDirectories[i], fileName);
                        if (File.Exists(search))
                        {
                            flagsPath = search;
                            break;
                        }
                    }
                }
            }

            if (flagsPath is null)
            {
                stderr.WriteLine($"error: flags file not found: {candidate}");
                return false;
            }
        }
        else if (!TryGetFullPath(flags, stderr, out flagsPath))
        {
            return false;
        }
        else if (!File.Exists(flagsPath))
        {
            stderr.WriteLine($"error: flags file not found: {flagsPath}");
            return false;
        }

        try
        {
            flagsFile = FlagsFile.Parse(File.ReadAllText(flagsPath));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            flagsFile = null;
            flagsPath = null;
            return false;
        }
    }

    private static bool TryCollectProjectScripts(
        PapyrusProject project,
        string projectDirectory,
        TextWriter stderr,
        out List<string> scripts)
    {
        scripts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var script in project.Scripts)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            var relative = script.Replace(':', Path.DirectorySeparatorChar);
            if (!Path.GetExtension(relative).Equals(".psc", StringComparison.OrdinalIgnoreCase))
            {
                relative += ".psc";
            }

            if (!TryResolveProjectPath(projectDirectory, relative, stderr, out var scriptPath))
            {
                return false;
            }

            if (!File.Exists(scriptPath))
            {
                stderr.WriteLine($"error: path not found: {scriptPath}");
                return false;
            }

            if (seen.Add(scriptPath))
            {
                scripts.Add(scriptPath);
            }
        }

        foreach (var folder in project.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Path))
            {
                continue;
            }

            if (!TryResolveProjectPath(projectDirectory, folder.Path, stderr, out var folderPath))
            {
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                stderr.WriteLine($"error: path not found: {folderPath}");
                return false;
            }

            List<string> found;
            try
            {
                found = new List<string>(Directory.EnumerateFiles(
                    folderPath,
                    "*.psc",
                    folder.NoRecurse ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException)
            {
                stderr.WriteLine($"error: {ex.Message}");
                return false;
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var scriptPath in found)
            {
                if (seen.Add(scriptPath))
                {
                    scripts.Add(scriptPath);
                }
            }
        }

        return true;
    }

    private static bool TryGetScriptOutputPath(
        string scriptPath,
        string? outputDirectory,
        List<string> includeDirectories,
        GameId game,
        TextWriter stderr,
        out string outputPath)
    {
        if (outputDirectory is null)
        {
            return TryGetFullPath(Path.ChangeExtension(scriptPath, ".pex"), stderr, out outputPath);
        }

        var name = GetScriptName(scriptPath, includeDirectories, game);
        if (name.Parts.Length == 0)
        {
            stderr.WriteLine("error: could not determine script name");
            outputPath = null!;
            return false;
        }

        var relative = Path.ChangeExtension(name.ToRelativePath(), ".pex")
            .Replace('/', Path.DirectorySeparatorChar);
        return TryGetFullPath(Path.Combine(outputDirectory, relative), stderr, out outputPath);
    }

    private static bool TryResolveProjectPath(
        string projectDirectory,
        string path,
        TextWriter stderr,
        out string fullPath)
    {
        if (Path.IsPathRooted(path))
        {
            return TryGetFullPath(path, stderr, out fullPath);
        }

        return TryGetFullPath(Path.Combine(projectDirectory, path), stderr, out fullPath);
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

    private static QualifiedName GetScriptName(string fullPath, List<string> imports, GameId game)
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
