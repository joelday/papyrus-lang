using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Benchmarks;

internal static class ParseThroughput
{
    internal const int WarmupIterations = 5;
    internal const int TimedIterations = 21;

    internal const string HelpText =
        """
        Usage: Papyrus.CodeAnalysis.Benchmarks [--corpus <path>]

          Measures SyntaxTree.Parse over committed Fixtures/Parse.
          Optional extra suite: --corpus, PAPYRUS_CORPUS, ./corpus, or the
          repo corpus/ directory when that path contains .psc files.

          Reports median/mean parse time, line counts, and process-wide RSS
          (WorkingSet64 of the whole process).
        """;

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParseArgs(args, out var corpusOverride, out var error))
        {
            stderr.WriteLine(error);
            return 1;
        }

        if (corpusOverride == HelpSentinel)
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        IReadOnlyList<ParseFixture> fixtures;
        try
        {
            fixtures = ParseFixtureCatalog.Load(ParseFixtureCatalog.FindDirectory());
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidOperationException)
        {
            stderr.WriteLine("error: " + ex.Message);
            return 1;
        }

        WriteHeader(stdout);
        MeasureSuite(stdout, "committed Fixtures/Parse", fixtures);

        if (!TryResolveCorpus(corpusOverride, stderr, out var corpus, out var corpusError))
        {
            stderr.WriteLine(corpusError);
            return 1;
        }

        if (corpus is null)
        {
            stdout.WriteLine();
            stdout.WriteLine("Maintainer corpus: not present.");
            stdout.WriteLine("Set --corpus, PAPYRUS_CORPUS, or place .psc files under ./corpus to measure it.");
            return 0;
        }

        IReadOnlyList<ParseFixture> corpusFixtures;
        try
        {
            corpusFixtures = ParseFixtureCatalog.Load(corpus);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidOperationException or IOException)
        {
            stderr.WriteLine("error: " + ex.Message);
            return 1;
        }

        stdout.WriteLine();
        MeasureSuite(stdout, "maintainer corpus (" + corpus + ")", corpusFixtures);
        return 0;
    }

    private const string HelpSentinel = "\0help";

    private static bool TryParseArgs(string[] args, out string? corpusOverride, out string error)
    {
        corpusOverride = null;
        error = null!;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help")
            {
                corpusOverride = HelpSentinel;
                return true;
            }

            if (arg == "--corpus")
            {
                if (i + 1 >= args.Length)
                {
                    error = "error: --corpus requires a path";
                    return false;
                }

                corpusOverride = args[++i];
                continue;
            }

            if (arg.StartsWith("--corpus=", StringComparison.Ordinal))
            {
                corpusOverride = arg["--corpus=".Length..];
                continue;
            }

            error = "error: unexpected argument '" + arg + "'";
            return false;
        }

        return true;
    }

    private static void WriteHeader(TextWriter stdout)
    {
        stdout.WriteLine("Parse throughput (short in-process job)");
        stdout.WriteLine("API: SyntaxTree.Parse(SourceText, PapyrusDialect)");
        stdout.WriteLine(
            "Iterations: " + WarmupIterations + " warmup + " + TimedIterations + " timed per script");
        stdout.WriteLine(
            "RSS: Process.WorkingSet64 after one retained parse of the suite (process-wide; includes runtime).");
        stdout.WriteLine("Runtime: " + Environment.Version + " / " + Environment.OSVersion);
        stdout.WriteLine();
    }

    private static void MeasureSuite(TextWriter stdout, string label, IReadOnlyList<ParseFixture> fixtures)
    {
        var scripts = new ScriptSample[fixtures.Count];
        var lineCounts = new int[fixtures.Count];
        var charCounts = new int[fixtures.Count];
        for (var i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            var lines = CountLines(fixture.Source);
            scripts[i] = new ScriptSample(fixture.Name, SourceText.From(fixture.Source), fixture.Dialect, lines);
            lineCounts[i] = lines;
            charCounts[i] = fixture.Source.Length;
        }

        Array.Sort(lineCounts);
        Array.Sort(charCounts);

        stdout.WriteLine("=== " + label + " ===");
        stdout.WriteLine("Scripts: " + fixtures.Count);
        stdout.WriteLine(
            "Lines: min " + lineCounts[0]
            + ", median " + FormatNumber(Median(lineCounts))
            + ", mean " + FormatNumber(Mean(lineCounts))
            + ", max " + lineCounts[^1]);
        stdout.WriteLine(
            "Chars: min " + charCounts[0]
            + ", median " + FormatNumber(Median(charCounts))
            + ", mean " + FormatNumber(Mean(charCounts))
            + ", max " + charCounts[^1]);
        stdout.WriteLine();
        stdout.WriteLine("Per-script median parse:");

        var medians = new double[scripts.Length];
        var means = new double[scripts.Length];
        var consumed = 0;
        for (var i = 0; i < scripts.Length; i++)
        {
            var sample = MeasureScript(scripts[i], ref consumed);
            medians[i] = sample.MedianMs;
            means[i] = sample.MeanMs;
            stdout.WriteLine(
                "  " + scripts[i].Name
                + "  " + scripts[i].Lines + " lines"
                + "  median " + FormatMs(sample.MedianMs)
                + "  mean " + FormatMs(sample.MeanMs));
        }

        Array.Sort(medians);
        Array.Sort(means);

        var onePass = TimeOnePass(scripts, ref consumed);
        var rss = MeasureRetainedRss(scripts, ref consumed);

        stdout.WriteLine();
        stdout.WriteLine("Suite:");
        stdout.WriteLine("  median of per-script medians: " + FormatMs(Median(medians)));
        stdout.WriteLine("  mean of per-script means:     " + FormatMs(Mean(means)));
        stdout.WriteLine("  max per-script median:        " + FormatMs(medians[^1]));
        stdout.WriteLine("  one-pass all scripts:         " + FormatMs(onePass));
        stdout.WriteLine(
            "  retained-tree WorkingSet64:   " + FormatMib(rss.WorkingSetBytes)
            + "  (process-wide)");
        stdout.WriteLine(
            "  WorkingSet64 delta:           " + FormatMib(rss.WorkingSetDeltaBytes)
            + "  (after retaining trees vs. after GC, still process-wide)");
        stdout.WriteLine("  retained-tree GC heap:        " + FormatMib(rss.GcHeapBytes));
        stdout.WriteLine("  consume-checksum:             " + consumed);
    }

    private static ScriptTiming MeasureScript(ScriptSample script, ref int consumed)
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            consumed += Consume(Parse(script));
        }

        var samples = new double[TimedIterations];
        for (var i = 0; i < TimedIterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var tree = Parse(script);
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            consumed += Consume(tree);
        }

        Array.Sort(samples);
        return new ScriptTiming(Median(samples), Mean(samples));
    }

    private static double TimeOnePass(ScriptSample[] scripts, ref int consumed)
    {
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < scripts.Length; i++)
        {
            consumed += Consume(Parse(scripts[i]));
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static RssSample MeasureRetainedRss(ScriptSample[] scripts, ref int consumed)
    {
        Collect();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var before = process.WorkingSet64;

        var trees = new SyntaxTree[scripts.Length];
        for (var i = 0; i < scripts.Length; i++)
        {
            trees[i] = Parse(scripts[i]);
            consumed += Consume(trees[i]);
        }

        Collect();
        process.Refresh();
        var after = process.WorkingSet64;
        var heap = GC.GetTotalMemory(forceFullCollection: false);
        GC.KeepAlive(trees);
        return new RssSample(after, after - before, heap);
    }

    private static SyntaxTree Parse(ScriptSample script)
    {
        return SyntaxTree.Parse(script.Text, script.Dialect, options: null);
    }

    private static int Consume(SyntaxTree tree)
    {
        return tree.GetRoot().FullSpan.Length;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static bool TryResolveCorpus(
        string? corpusOverride,
        TextWriter stderr,
        out string? corpus,
        out string error)
    {
        corpus = null;
        error = null!;

        if (!string.IsNullOrWhiteSpace(corpusOverride))
        {
            if (!HasPsc(corpusOverride))
            {
                error = "error: --corpus is not a directory of .psc files: " + corpusOverride;
                return false;
            }

            corpus = Path.GetFullPath(corpusOverride);
            return true;
        }

        var env = Environment.GetEnvironmentVariable("PAPYRUS_CORPUS");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (HasPsc(env))
            {
                corpus = Path.GetFullPath(env);
                return true;
            }

            stderr.WriteLine("warning: PAPYRUS_CORPUS is set but is not a directory of .psc files: " + env);
        }

        var cwdCorpus = Path.Combine(Directory.GetCurrentDirectory(), "corpus");
        if (HasPsc(cwdCorpus))
        {
            corpus = cwdCorpus;
            return true;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "corpus");
            if (HasPsc(candidate))
            {
                corpus = candidate;
                return true;
            }

            if (IsRepoRoot(dir.FullName))
            {
                break;
            }

            dir = dir.Parent;
        }

        return true;
    }

    private static bool IsRepoRoot(string path)
    {
        return File.Exists(Path.Combine(path, "Papyrus.sln"))
            || Directory.Exists(Path.Combine(path, ".git"))
            || File.Exists(Path.Combine(path, ".git"));
    }

    private static bool HasPsc(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        foreach (var _ in Directory.EnumerateFiles(path, "*.psc", SearchOption.AllDirectories))
        {
            return true;
        }

        return false;
    }

    private static int CountLines(string source)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                lines++;
            }
        }

        if (source[^1] == '\n')
        {
            lines--;
        }

        return lines;
    }

    private static double Median(int[] sorted)
    {
        var n = sorted.Length;
        if ((n & 1) == 1)
        {
            return sorted[n / 2];
        }

        return (sorted[n / 2 - 1] + (double)sorted[n / 2]) / 2.0;
    }

    private static double Median(double[] sorted)
    {
        var n = sorted.Length;
        if ((n & 1) == 1)
        {
            return sorted[n / 2];
        }

        return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static double Mean(int[] values)
    {
        var sum = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        return sum / values.Length;
    }

    private static double Mean(double[] values)
    {
        var sum = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        return sum / values.Length;
    }

    private static string FormatMs(double milliseconds)
    {
        return milliseconds.ToString("0.0000", CultureInfo.InvariantCulture) + " ms";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatMib(long bytes)
    {
        return (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB";
    }

    private sealed class ScriptSample
    {
        internal ScriptSample(string name, SourceText text, PapyrusDialect dialect, int lines)
        {
            Name = name;
            Text = text;
            Dialect = dialect;
            Lines = lines;
        }

        internal string Name { get; }
        internal SourceText Text { get; }
        internal PapyrusDialect Dialect { get; }
        internal int Lines { get; }
    }

    private readonly struct ScriptTiming
    {
        internal ScriptTiming(double medianMs, double meanMs)
        {
            MedianMs = medianMs;
            MeanMs = meanMs;
        }

        internal double MedianMs { get; }
        internal double MeanMs { get; }
    }

    private readonly struct RssSample
    {
        internal RssSample(long workingSetBytes, long workingSetDeltaBytes, long gcHeapBytes)
        {
            WorkingSetBytes = workingSetBytes;
            WorkingSetDeltaBytes = workingSetDeltaBytes;
            GcHeapBytes = gcHeapBytes;
        }

        internal long WorkingSetBytes { get; }
        internal long WorkingSetDeltaBytes { get; }
        internal long GcHeapBytes { get; }
    }
}
