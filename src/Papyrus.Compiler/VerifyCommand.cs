using System;
using System.IO;
using System.Text.Json;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.Pex;

namespace Papyrus.Compiler;

internal sealed class VerifyCommandOptions
{
    internal VerifyCommandOptions(
        string source,
        string? game,
        string? baseline,
        string? dest,
        string? attestation,
        bool writeBaselines,
        bool compile = false,
        string? allowlist = null,
        string? ckCompiler = null)
    {
        Source = source;
        Game = game;
        Baseline = baseline;
        Dest = dest;
        Attestation = attestation;
        WriteBaselines = writeBaselines;
        Compile = compile;
        Allowlist = allowlist;
        CkCompiler = ckCompiler;
    }

    internal string Source { get; }
    internal string? Game { get; }
    internal string? Baseline { get; }
    internal string? Dest { get; }
    internal string? Attestation { get; }
    internal bool WriteBaselines { get; }
    internal bool Compile { get; }
    internal string? Allowlist { get; }
    internal string? CkCompiler { get; }
}

internal static class VerifyCommand
{
    internal static int Execute(VerifyCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (!string.IsNullOrWhiteSpace(options.CkCompiler))
        {
            stderr.WriteLine(
                "error: --ck-compiler (tier-2) is not implemented; use --compile for compile-vs-baseline. See docs/compliance/baselines/README.md");
            return 1;
        }

        GameId? preferred = null;
        string gameName = options.Game ?? "unknown";
        if (options.Game is not null)
        {
            if (!GameNames.TryParse(options.Game, out gameName, out var game))
            {
                stderr.WriteLine("error: --game must be one of: skyrimse, fallout4, starfield, skyrimle");
                return 1;
            }

            preferred = game;
        }

        if (options.Compile)
            return ExecuteCompile(options, gameName, stdout, stderr);

        string source;
        try
        {
            source = Path.GetFullPath(options.Source);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!Directory.Exists(source) && !File.Exists(source))
        {
            stderr.WriteLine($"error: source not found: {source}");
            return 1;
        }

        try
        {
            var entries = PexHashHarness.HashPath(source, preferred);

            if (options.WriteBaselines)
                return WriteBaselines(options, gameName, entries, stdout, stderr);

            return Compare(options.Baseline!, options.Game, entries, stdout, stderr);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PexFormatException
            or InvalidOperationException or DirectoryNotFoundException or JsonException
            or ArgumentException or NotSupportedException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int ExecuteCompile(VerifyCommandOptions options, string gameName, TextWriter stdout, TextWriter stderr)
    {
        var allowlist = LoadAllowlist(options.Allowlist, stderr);
        if (allowlist is null)
            return 1;

        string source = options.Source;
        if (string.IsNullOrWhiteSpace(source))
            source = CompileHashHarness.DefaultCorpusPath(
                string.Equals(gameName, "unknown", StringComparison.OrdinalIgnoreCase) ? null : gameName);

        if (!CompileHashHarness.IsCorpusPresent(source))
        {
            stdout.WriteLine("allowlist size: " + allowlist.Size);
            stdout.WriteLine(CompileHashHarness.SkipReason(source));
            return 0;
        }

        if (!GameNames.TryGetDialect(options.Game, out _, out var dialect))
        {
            stderr.WriteLine("error: --game is required with --compile and must be one of: skyrimse, fallout4, starfield, skyrimle");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.Baseline))
        {
            stderr.WriteLine("error: --baseline is required with --compile when a corpus is present");
            return 1;
        }

        try
        {
            string sourceFull = Path.GetFullPath(source);
            string baselineFull = Path.GetFullPath(options.Baseline);
            if (!File.Exists(baselineFull))
            {
                stderr.WriteLine($"error: baseline not found: {baselineFull}");
                return 1;
            }

            var compiled = CompileHashHarness.CompilePath(sourceFull, dialect);
            var document = BaselineFile.Load(baselineFull);
            if (!string.IsNullOrEmpty(document.Game)
                && !string.Equals(document.Game, gameName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(document.Game, "unknown", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(document.Game, "synthetic", StringComparison.OrdinalIgnoreCase))
            {
                stderr.WriteLine($"error: --game {gameName} does not match baseline game '{document.Game}'");
                return 1;
            }

            var result = CompileHashHarness.Compare(compiled, document, allowlist);
            stdout.WriteLine("allowlist size: " + result.AllowlistSize);
            stdout.WriteLine(result.ComplianceLine);
            stdout.WriteLine("allowlisted: " + result.Allowlisted);
            if (result.CompileFailed > 0)
                stdout.WriteLine("compile failed: " + result.CompileFailed);
            if (result.Mismatch > 0)
                stdout.WriteLine("mismatch: " + result.Mismatch);

            if (result.IsGatePass)
            {
                stdout.WriteLine($"OK: {result.Identical}/{result.Total} identical");
                return 0;
            }

            foreach (var error in result.Errors)
                stderr.WriteLine($"error: {error}");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PexFormatException
            or InvalidOperationException or DirectoryNotFoundException or JsonException
            or ArgumentException or NotSupportedException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static AllowlistDocument? LoadAllowlist(string? path, TextWriter stderr)
    {
        string? resolved = path;
        if (string.IsNullOrWhiteSpace(resolved))
            resolved = AllowlistFile.FindDefaultPath();

        if (string.IsNullOrWhiteSpace(resolved))
            return new AllowlistDocument([]);

        string full;
        try
        {
            full = Path.GetFullPath(resolved);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return null;
        }

        if (!File.Exists(full))
        {
            if (path is not null)
            {
                stderr.WriteLine($"error: allowlist not found: {full}");
                return null;
            }

            return new AllowlistDocument([]);
        }

        try
        {
            return AllowlistFile.Load(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return null;
        }
    }

    private static int Compare(
        string baselinePath,
        string? expectedGame,
        System.Collections.Immutable.ImmutableArray<PexHashEntry> entries,
        TextWriter stdout,
        TextWriter stderr)
    {
        string full;
        try
        {
            full = Path.GetFullPath(baselinePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!File.Exists(full))
        {
            stderr.WriteLine($"error: baseline not found: {full}");
            return 1;
        }

        var document = BaselineFile.Load(full);
        if (expectedGame is not null &&
            !string.IsNullOrEmpty(document.Game) &&
            !string.Equals(document.Game, expectedGame, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(document.Game, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine($"error: --game {expectedGame} does not match baseline game '{document.Game}'");
            return 1;
        }

        var comparison = BaselineFile.Compare(document, entries);
        if (comparison.IsMatch)
        {
            stdout.WriteLine($"OK: {entries.Length} hash(es) match {full}");
            return 0;
        }

        foreach (var error in comparison.Errors)
            stderr.WriteLine($"error: {error}");
        return 1;
    }

    private static int WriteBaselines(
        VerifyCommandOptions options,
        string gameName,
        System.Collections.Immutable.ImmutableArray<PexHashEntry> entries,
        TextWriter stdout,
        TextWriter stderr)
    {
        // Maintainer-only: refuse to emit hashes without a parsed attestation note.
        string attestationPath;
        try
        {
            attestationPath = Path.GetFullPath(options.Attestation!);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (!File.Exists(attestationPath))
        {
            stderr.WriteLine($"error: attestation note not found: {attestationPath}");
            return 1;
        }

        string text = File.ReadAllText(attestationPath);
        if (!AttestationNote.TryParse(text, out var note, out var parseError))
        {
            stderr.WriteLine($"error: {parseError}");
            return 1;
        }

        if (string.Equals(note.PapyruscVersion, "auto", StringComparison.OrdinalIgnoreCase))
        {
            note = new AttestationNote(
                note.Os,
                Cli.Version,
                note.CkCompilerPathSha256,
                note.CkFileVersion,
                note.Statement);
        }

        string dest;
        try
        {
            dest = Path.GetFullPath(options.Dest!);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }

        BaselineFile.WriteBaselines(dest, gameName, entries, note);
        stdout.WriteLine($"Wrote {entries.Length} hash(es) to {dest}");
        return 0;
    }
}
