using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Papyrus.Workspaces;

namespace Papyrus.Compiler;

/// <summary>
/// Tier-3 round-trip: decompile vanilla .pex, recompile with ChampollionCompat, compare hashes.
/// </summary>
internal static class ChampollionCommand
{
    internal const string PathEnvironmentVariable = "PAPYRUSC_CHAMPOLLION";
    private const int DecompileTimeoutMs = 600_000;

    internal static int Execute(VerifyCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
        string source;
        try
        {
            source = ResolveSource(options);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!Directory.Exists(source) && !File.Exists(source))
        {
            stdout.WriteLine($"skip: source not found: {source}");
            return 0;
        }

        List<string> pexFiles;
        try
        {
            pexFiles = ListPex(source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (pexFiles.Count == 0)
        {
            stdout.WriteLine($"skip: no .pex files in {source}");
            return 0;
        }

        if (!TryFindBinary(options.ChampollionPath, out var binary))
        {
            stdout.WriteLine("skip: Champollion binary not found (set PAPYRUSC_CHAMPOLLION or --champollion-path)");
            return 0;
        }

        GameId? preferred = null;
        PapyrusDialect? forcedDialect = null;
        if (options.Game is not null)
        {
            if (!GameNames.TryParse(options.Game, out _, out var game) || !CompileCommand.TryGetDialect(game, out forcedDialect))
            {
                stderr.WriteLine("error: --game must be one of: skyrimse, fallout4, starfield, skyrimle");
                return 1;
            }

            preferred = game;
        }

        var work = Path.Combine(Path.GetTempPath(), "papyrusc-champollion-" + Guid.NewGuid().ToString("N"));
        var pscDir = Path.Combine(work, "psc");
        try
        {
            Directory.CreateDirectory(pscDir);
            var decompile = Decompile(binary, source, pscDir, stderr);
            if (decompile != 0)
                return decompile;

            var matched = 0;
            var mismatched = 0;
            var failed = 0;
            var sourceRoot = Directory.Exists(source) ? Path.GetFullPath(source) : Path.GetDirectoryName(Path.GetFullPath(source)) ?? source;

            foreach (var pexPath in pexFiles)
            {
                var relative = NormalizeRelative(
                    Directory.Exists(source)
                        ? Path.GetRelativePath(sourceRoot, pexPath)
                        : Path.GetFileName(pexPath));

                if (!TryRoundTripOne(
                    pexPath,
                    relative,
                    pscDir,
                    preferred,
                    forcedDialect,
                    out var originalHash,
                    out var compiledHash,
                    stderr))
                {
                    failed++;
                    continue;
                }

                if (string.Equals(originalHash, compiledHash, StringComparison.Ordinal))
                {
                    stdout.WriteLine($"OK: {relative}");
                    matched++;
                }
                else
                {
                    stderr.WriteLine($"error: hash mismatch: {relative}");
                    mismatched++;
                }
            }

            stdout.WriteLine(
                $"Champollion round-trip: {matched.ToString(CultureInfo.InvariantCulture)} match, " +
                $"{mismatched.ToString(CultureInfo.InvariantCulture)} mismatch, " +
                $"{failed.ToString(CultureInfo.InvariantCulture)} error");
            return mismatched == 0 && failed == 0 ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PexFormatException
            or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    internal static bool TryFindBinary(string? explicitPath, out string path)
    {
        path = null!;
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return TryExistingFile(explicitPath, out path);

        if (TryExistingFile(Environment.GetEnvironmentVariable(PathEnvironmentVariable), out path))
            return true;

        if (TryFindOnPath("champollion", out path)
            || TryFindOnPath("Champollion", out path)
            || TryFindOnPath("Champollion.exe", out path))
            return true;

        foreach (var candidate in DocumentedPaths())
        {
            if (TryExistingFile(candidate, out path))
                return true;
        }

        return false;
    }

    internal static string HashForRoundTrip(ReadOnlySpan<byte> pex, GameId? preferred = null)
        => HashForRoundTrip(PexReader.Read(pex, preferred));

    internal static string HashForRoundTrip(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        file.DebugInfo = null;
        return PexNormalizer.Hash(file);
    }

    private static string ResolveSource(VerifyCommandOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Source))
            return Path.GetFullPath(options.Source);

        if (options.Game is not null && GameNames.TryParse(options.Game, out var game, out _))
            return Path.GetFullPath(Path.Combine("corpus", game));

        return Path.GetFullPath("corpus");
    }

    private static int Decompile(string binary, string source, string pscDir, TextWriter stderr)
    {
        var start = new ProcessStartInfo
        {
            FileName = binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(source);
        start.ArgumentList.Add("-p");
        start.ArgumentList.Add(pscDir);
        if (Directory.Exists(source))
        {
            start.ArgumentList.Add("-r");
            start.ArgumentList.Add("-t");
        }

        start.ArgumentList.Add("--no-debug-line");

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                stderr.WriteLine("error: failed to start Champollion");
                return 1;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(DecompileTimeoutMs))
            {
                TryKill(process);
                stderr.WriteLine("error: Champollion timed out");
                return 1;
            }

            _ = stdoutTask.GetAwaiter().GetResult();
            var errorOutput = stderrTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                stderr.WriteLine("error: Champollion exited " + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(errorOutput))
                    stderr.WriteLine(errorOutput.TrimEnd());
                return 1;
            }

            return 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static bool TryRoundTripOne(
        string pexPath,
        string relative,
        string pscDir,
        GameId? preferred,
        PapyrusDialect? forcedDialect,
        out string originalHash,
        out string compiledHash,
        TextWriter stderr)
    {
        originalHash = "";
        compiledHash = "";
        var originalBytes = File.ReadAllBytes(pexPath);
        var original = PexReader.Read(originalBytes, preferred);
        originalHash = HashForRoundTrip(original);

        var dialect = forcedDialect;
        if (dialect is null && !CompileCommand.TryGetDialect(original.Game, out dialect))
        {
            stderr.WriteLine($"error: unsupported game in {relative}");
            return false;
        }

        var objectName = original.Objects.Length > 0 ? original.Objects[0].Name : null;
        if (!TryFindDecompiledScript(pscDir, relative, objectName, out var pscPath))
        {
            stderr.WriteLine($"error: decompiled source not found for {relative}");
            return false;
        }

        if (!TryCompile(pscPath, pscDir, dialect, objectName, out var compiled, stderr))
            return false;

        compiledHash = HashForRoundTrip(compiled, dialect.Game);
        return true;
    }

    internal static bool TryCompile(
        string scriptPath,
        string pscRoot,
        PapyrusDialect dialect,
        string? objectName,
        out byte[] bytes,
        TextWriter stderr)
    {
        bytes = null!;
        var imports = new List<string> { pscRoot };
        var name = NameFromObject(objectName);
        if (name.Parts.Length == 0)
            name = CompileCommand.GetScriptName(scriptPath, imports, dialect.Game);
        if (name.Parts.Length == 0)
        {
            stderr.WriteLine($"error: could not determine script name: {scriptPath}");
            return false;
        }

        FileSystemScriptResolver resolver;
        try
        {
            resolver = new FileSystemScriptResolver(imports, outputDirectory: null, dialect.Game);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return false;
        }

        var text = SourceText.From(File.ReadAllBytes(scriptPath));
        var compilation = PapyrusCompilation.Create(
                dialect,
                resolver,
                new PapyrusParseOptions { ChampollionCompat = true })
            .WithScriptText(name, text);
        var script = compilation.GetScript(name);
        var emitter = new PexEmitter(options: new PexEmitOptions { EmitDebugInfo = false });
        var pex = emitter.EmitFile(script);
        pex.CompileTime = DateTimeOffset.UnixEpoch;
        pex.DebugInfo = null;

        var hasError = false;
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            hasError = true;
            stderr.WriteLine($"{scriptPath}: error {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        foreach (var diagnostic in emitter.Diagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            hasError = true;
            stderr.WriteLine($"{scriptPath}: error {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        if (hasError)
            return false;

        bytes = PexWriter.Write(pex);
        return true;
    }

    internal static QualifiedName NameFromObject(string? objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return default;

        var text = PexQualifiedName.Parse(objectName).Text;
        return string.IsNullOrEmpty(text) ? default : QualifiedName.Parse(text);
    }

    internal static bool TryFindDecompiledScript(string pscDir, string relativePex, string? objectName, out string pscPath)
    {
        pscPath = null!;
        var relativePsc = Path.ChangeExtension(relativePex.Replace('/', Path.DirectorySeparatorChar), ".psc");
        if (TryExistingFile(Path.Combine(pscDir, relativePsc), out pscPath))
            return true;

        if (!string.IsNullOrEmpty(objectName))
        {
            var fromObject = objectName.Replace(':', Path.DirectorySeparatorChar) + ".psc";
            if (TryExistingFile(Path.Combine(pscDir, fromObject), out pscPath))
                return true;
        }

        var fileName = Path.ChangeExtension(Path.GetFileName(relativePex), ".psc");
        if (TryExistingFile(Path.Combine(pscDir, fileName), out pscPath))
            return true;

        string? found = null;
        foreach (var file in Directory.EnumerateFiles(pscDir, "*.psc", SearchOption.AllDirectories))
        {
            if (!string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (found is not null)
            {
                pscPath = null!;
                return false;
            }

            found = file;
        }

        if (found is null)
            return false;

        pscPath = found;
        return true;
    }

    private static List<string> ListPex(string source)
    {
        var files = new List<string>();
        var full = Path.GetFullPath(source);
        if (File.Exists(full))
        {
            if (full.EndsWith(".pex", StringComparison.OrdinalIgnoreCase))
                files.Add(full);
            return files;
        }

        foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".pex", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(full, file);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
                continue;

            files.Add(file);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static IEnumerable<string> DocumentedPaths()
    {
        yield return Path.Combine("tools", "Champollion");
        yield return Path.Combine("tools", "champollion");
        yield return Path.Combine("tools", "Champollion.exe");

        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir))
            yield break;

        yield return Path.Combine(baseDir, "Champollion");
        yield return Path.Combine(baseDir, "champollion");
        yield return Path.Combine(baseDir, "Champollion.exe");
        yield return Path.Combine(baseDir, "tools", "Champollion");
        yield return Path.Combine(baseDir, "tools", "Champollion.exe");
    }

    private static bool TryFindOnPath(string name, out string path)
    {
        path = null!;
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return false;

        foreach (var directory in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            if (TryExistingFile(Path.Combine(directory, name), out path))
                return true;
        }

        return false;
    }

    private static bool TryExistingFile(string? candidate, out string path)
    {
        path = null!;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        try
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                path = full;
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return false;
    }

    private static string NormalizeRelative(string relative) => relative.Replace('\\', '/');

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
