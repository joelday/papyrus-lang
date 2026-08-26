using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Testing;

public sealed class CompiledPexHash
{
    public CompiledPexHash(
        string relativePath,
        string scriptName,
        string? sha256,
        ImmutableArray<string> errors)
    {
        RelativePath = relativePath;
        ScriptName = scriptName;
        Sha256 = sha256;
        Errors = errors.IsDefault ? ImmutableArray<string>.Empty : errors;
    }

    public string RelativePath { get; }

    public string ScriptName { get; }

    public string? Sha256 { get; }

    public ImmutableArray<string> Errors { get; }

    public bool Succeeded => Sha256 is not null && Errors.Length == 0;
}

public sealed class CompileVerifyResult
{
    public CompileVerifyResult(
        bool skipped,
        string? skipReason,
        int allowlistSize,
        int identical,
        int allowlisted,
        int mismatch,
        int compileFailed,
        int total,
        ImmutableArray<string> errors)
    {
        Skipped = skipped;
        SkipReason = skipReason;
        AllowlistSize = allowlistSize;
        Identical = identical;
        Allowlisted = allowlisted;
        Mismatch = mismatch;
        CompileFailed = compileFailed;
        Total = total;
        Errors = errors.IsDefault ? ImmutableArray<string>.Empty : errors;
    }

    public bool Skipped { get; }

    public string? SkipReason { get; }

    public int AllowlistSize { get; }

    public int Identical { get; }

    public int Allowlisted { get; }

    public int Mismatch { get; }

    public int CompileFailed { get; }

    public int Total { get; }

    public ImmutableArray<string> Errors { get; }

    public bool IsGatePass => !Skipped && Errors.Length == 0;

    public string ComplianceLine
    {
        get
        {
            if (Total <= 0)
                return "identical: 0/0";

            var percent = (int)Math.Round(100.0 * Identical / Total, MidpointRounding.AwayFromZero);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"identical: {Identical}/{Total} ({percent}%)");
        }
    }
}

/// <summary>
/// Compiles <c>.psc</c> with <see cref="PexEmitter"/> and hashes normalized PEX.
/// Public CI skips when the local corpus is absent.
/// </summary>
public static class CompileHashHarness
{
    public const string SkipSuffix = " (public CI does not require game files)";

    public static bool IsCorpusPresent(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (File.Exists(full))
            return IsPsc(full);

        if (!Directory.Exists(full))
            return false;

        return ListPsc(full).Count > 0;
    }

    public static string SkipReason(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "skip: corpus not found" + SkipSuffix;
        }

        if (Directory.Exists(full))
            return "skip: no .psc files in " + full + SkipSuffix;

        return "skip: corpus not found at " + full + SkipSuffix;
    }

    public static string DefaultCorpusPath(string? game, string? startDirectory = null)
    {
        string root = FindRepoRoot(startDirectory) ?? startDirectory ?? Directory.GetCurrentDirectory();
        return string.IsNullOrWhiteSpace(game)
            ? Path.Combine(root, "corpus")
            : Path.Combine(root, "corpus", game);
    }

    public static string HashScript(
        string source,
        PapyrusDialect? dialect = null,
        string scriptName = "Foo")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        dialect ??= PapyrusDialect.SkyrimSe;
        var name = QualifiedName.Parse(scriptName);
        var compilation = PapyrusCompilation.Create(
            dialect,
            new MemoryScriptResolver(
            [
                new KeyValuePair<QualifiedName, SourceText>(name, SourceText.From(source)),
            ]));
        var script = compilation.GetScript(name);
        var emitter = new PexEmitter();
        var pex = emitter.EmitFile(script);
        if (HasError(compilation.GetDiagnostics()) || HasError(emitter.Diagnostics))
            throw new InvalidOperationException("emit produced diagnostics.");

        return PexNormalizer.Hash(pex);
    }

    public static ImmutableArray<CompiledPexHash> CompilePath(string path, PapyrusDialect dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(dialect);

        string full = Path.GetFullPath(path);
        if (File.Exists(full))
        {
            if (!IsPsc(full))
                throw new InvalidOperationException("not a .psc file: " + full);

            string directory = Path.GetDirectoryName(full) ?? full;
            return CompileFiles(directory, [full], dialect);
        }

        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException("source not found: " + full);

        return CompileFiles(full, ListPsc(full), dialect);
    }

    public static CompileVerifyResult Skip(string path, int allowlistSize)
    {
        return new CompileVerifyResult(
            skipped: true,
            skipReason: SkipReason(path),
            allowlistSize,
            identical: 0,
            allowlisted: 0,
            mismatch: 0,
            compileFailed: 0,
            total: 0,
            errors: []);
    }

    public static CompileVerifyResult Compare(
        ImmutableArray<CompiledPexHash> compiled,
        BaselineDocument baseline,
        AllowlistDocument? allowlist = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        allowlist ??= new AllowlistDocument([]);

        var actual = new Dictionary<string, CompiledPexHash>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in compiled)
            actual[entry.ScriptName] = entry;

        var errors = ImmutableArray.CreateBuilder<string>();
        var identical = 0;
        var allowlisted = 0;
        var mismatch = 0;
        var compileFailed = 0;

        var expectedNames = new List<string>(baseline.Scripts.Keys);
        expectedNames.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string name in expectedNames)
        {
            var script = baseline.Scripts[name];
            if (!actual.TryGetValue(name, out var entry))
            {
                errors.Add("missing from source: " + name);
                continue;
            }

            var waived = IsAllowlisted(script, name, allowlist);
            if (!entry.Succeeded)
            {
                compileFailed++;
                if (waived)
                {
                    allowlisted++;
                    continue;
                }

                var detail = entry.Errors.Length == 0
                    ? "compile failed: " + name
                    : "compile failed: " + name + ": " + entry.Errors[0];
                errors.Add(detail);
                continue;
            }

            if (string.Equals(script.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                identical++;
                continue;
            }

            if (waived)
            {
                allowlisted++;
                continue;
            }

            mismatch++;
            errors.Add(
                "hash mismatch: " + name + Environment.NewLine
                + "  expected: " + script.Sha256 + Environment.NewLine
                + "  actual:   " + entry.Sha256);
        }

        var extra = new List<string>();
        foreach (string name in actual.Keys)
        {
            if (!baseline.Scripts.ContainsKey(name))
                extra.Add(name);
        }

        extra.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string name in extra)
            errors.Add("missing from baseline: " + name);

        return new CompileVerifyResult(
            skipped: false,
            skipReason: null,
            allowlistSize: allowlist.Size,
            identical,
            allowlisted,
            mismatch,
            compileFailed,
            total: expectedNames.Count,
            errors: errors.ToImmutable());
    }

    internal static List<string> ListPsc(string source)
    {
        var files = new List<string>();
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (!IsPsc(file))
                continue;

            string relative = Path.GetRelativePath(source, file);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
                continue;

            files.Add(file);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    internal static bool IsPsc(string path) =>
        path.EndsWith(".psc", StringComparison.OrdinalIgnoreCase);

    private static ImmutableArray<CompiledPexHash> CompileFiles(
        string root,
        List<string> files,
        PapyrusDialect dialect)
    {
        var sources = new List<KeyValuePair<QualifiedName, SourceText>>(files.Count);
        var names = new List<(string Relative, string ScriptName, QualifiedName Name)>(files.Count);
        var seen = new Dictionary<QualifiedName, string>();

        foreach (string file in files)
        {
            string relative = PexHashHarness.NormalizeRelative(Path.GetRelativePath(root, file));
            string scriptName = ScriptName(relative);
            var name = QualifiedName.Parse(scriptName.Replace('/', ':'));
            if (name.Parts.Length == 0)
                continue;

            if (seen.TryGetValue(name, out var previous))
            {
                throw new InvalidOperationException(
                    "duplicate script name '" + name + "': " + previous + " and " + relative);
            }

            seen[name] = relative;
            names.Add((relative, scriptName, name));
            sources.Add(new KeyValuePair<QualifiedName, SourceText>(name, SourceText.From(File.ReadAllBytes(file))));
        }

        var compilation = PapyrusCompilation.Create(dialect, new MemoryScriptResolver(sources));
        var builder = ImmutableArray.CreateBuilder<CompiledPexHash>(names.Count);
        foreach (var (relative, scriptName, name) in names)
            builder.Add(CompileOne(compilation, relative, scriptName, name));

        return builder.MoveToImmutable();
    }

    private static CompiledPexHash CompileOne(
        PapyrusCompilation compilation,
        string relative,
        string scriptName,
        QualifiedName name)
    {
        try
        {
            var script = compilation.GetScript(name);
            var tree = compilation.GetSyntaxTree(name);
            var emitter = new PexEmitter();
            var pex = emitter.EmitFile(script);

            var errors = ImmutableArray.CreateBuilder<string>();
            CollectErrors(compilation.GetDiagnostics(), tree, errors);
            CollectErrors(emitter.Diagnostics, tree, errors);
            if (errors.Count > 0)
                return new CompiledPexHash(relative, scriptName, sha256: null, errors.ToImmutable());

            return new CompiledPexHash(relative, scriptName, PexNormalizer.Hash(pex), []);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PexFormatException or ArgumentException)
        {
            return new CompiledPexHash(relative, scriptName, sha256: null, [ex.Message]);
        }
    }

    private static void CollectErrors(
        ImmutableArray<Diagnostic> diagnostics,
        SyntaxTree tree,
        ImmutableArray<string>.Builder errors)
    {
        if (diagnostics.IsDefaultOrEmpty)
            return;

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
                continue;

            if (diagnostic.Tree is not null && !ReferenceEquals(diagnostic.Tree, tree))
                continue;

            errors.Add(diagnostic.Id + ": " + diagnostic.GetMessage());
        }
    }

    private static bool HasError(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsDefaultOrEmpty)
            return false;

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                return true;
        }

        return false;
    }

    private static bool IsAllowlisted(BaselineScript script, string scriptName, AllowlistDocument allowlist)
    {
        if (string.Equals(script.Status, "allowlisted", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(script.AllowlistId) && allowlist.ContainsId(script.AllowlistId))
            return true;

        return allowlist.ContainsScript(scriptName);
    }

    private static string ScriptName(string relativePath)
    {
        string name = relativePath.Replace('\\', '/');
        if (name.EndsWith(".psc", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }

    private static string? FindRepoRoot(string? startDirectory)
    {
        DirectoryInfo? dir;
        try
        {
            dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Papyrus.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
