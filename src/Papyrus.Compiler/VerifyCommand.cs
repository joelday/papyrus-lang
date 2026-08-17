using System;
using System.IO;
using System.Reflection;
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
        bool writeBaselines)
    {
        Source = source;
        Game = game;
        Baseline = baseline;
        Dest = dest;
        Attestation = attestation;
        WriteBaselines = writeBaselines;
    }

    internal string Source { get; }
    internal string? Game { get; }
    internal string? Baseline { get; }
    internal string? Dest { get; }
    internal string? Attestation { get; }
    internal bool WriteBaselines { get; }
}

internal static class VerifyCommand
{
    internal static int Execute(VerifyCommandOptions options, TextWriter stdout, TextWriter stderr)
    {
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

            if (!string.IsNullOrWhiteSpace(options.Baseline))
                return Compare(options.Baseline, options.Game, entries, stdout, stderr);

            foreach (var entry in entries)
                stdout.WriteLine($"{entry.Sha256}  {entry.RelativePath}");
            stdout.WriteLine($"Hashed {entries.Length} .pex file(s). Does not compile.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PexFormatException or InvalidOperationException or DirectoryNotFoundException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
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
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                note.CkCompilerPathSha256,
                note.CkFileVersion,
                note.Statement);
        }

        string dest = Path.GetFullPath(options.Dest!);
        BaselineFile.WriteBaselines(dest, gameName, entries, note);
        stdout.WriteLine($"Wrote {entries.Length} hash(es) to {dest}");
        return 0;
    }
}
