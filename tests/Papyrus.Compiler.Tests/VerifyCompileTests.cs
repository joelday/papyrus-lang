using System;
using System.IO;
using Papyrus.CodeAnalysis.Testing;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class VerifyCompileTests
{
    private const string TinyScript = "ScriptName Foo\nFunction Bar()\nEndFunction\n";

    [Fact]
    public void Help_MentionsCompileVerify()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("verify --compile", stdout, StringComparison.Ordinal);
        Assert.Contains("--allowlist", stdout, StringComparison.Ordinal);
        Assert.Contains("--flags", stdout, StringComparison.Ordinal);
        Assert.Contains("--ck-compiler", stdout, StringComparison.Ordinal);
        Assert.Contains("Skips (exit 0) when the local corpus is absent", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Verify_Compile_MissingCorpus_SkipsZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "corpus", "skyrimse");

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", missing);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("allowlist size:", stdout, StringComparison.Ordinal);
        Assert.Contains("skip:", stdout, StringComparison.Ordinal);
        Assert.Contains("public CI does not require game files", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_EmptyDirectory_SkipsZero()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(root.Path);

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("skip:", stdout, StringComparison.Ordinal);
        Assert.Contains("public CI does not require game files", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_CkCompiler_ExitsNonZero()
    {
        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--ck-compiler", "PapyrusCompiler.exe",
            "--source", ".");

        Assert.Equal(1, code);
        Assert.Contains("--ck-compiler", stderr, StringComparison.Ordinal);
        Assert.Contains("not implemented", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_SyntheticScript_MatchesBaseline()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var hash = CompileHashHarness.HashScript(TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            $$"""
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "{{hash}}", "status": "identical" }
              }
            }
            """);

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("identical: 1/1", stdout, StringComparison.Ordinal);
        Assert.Contains("OK:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_HashMismatch_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            """
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "status": "identical" }
              }
            }
            """);

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(1, code);
        Assert.Contains("hash mismatch: Foo", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_AllowlistedMismatch_IsNotAGateFailure()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            """
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "status": "allowlisted", "allowlistId": "none-to-bool-string" }
              }
            }
            """);
        var allowlist = Path.Combine(root.Path, "allowlist.md");
        File.WriteAllText(
            allowlist,
            "1. `none-to-bool-string` — None to Bool is semantic-equivalence only.\n");

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline,
            "--allowlist", allowlist);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("allowlisted: 1", stdout, StringComparison.Ordinal);
        Assert.Contains("allowlist size: 1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_StatusAllowlistedWithoutId_IsGateFailure()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            """
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "status": "allowlisted" }
              }
            }
            """);
        var allowlist = Path.Combine(root.Path, "allowlist.md");
        File.WriteAllText(
            allowlist,
            "1. `none-to-bool-string` — None to Bool is semantic-equivalence only.\n");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline,
            "--allowlist", allowlist);

        Assert.Equal(1, code);
        Assert.Contains("hash mismatch: Foo", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_UnknownAllowlistId_IsGateFailure()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var hash = CompileHashHarness.HashScript(TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            $$"""
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "{{hash}}", "status": "allowlisted", "allowlistId": "not-a-real-id" }
              }
            }
            """);
        var allowlist = Path.Combine(root.Path, "allowlist.md");
        File.WriteAllText(
            allowlist,
            "1. `none-to-bool-string` — None to Bool is semantic-equivalence only.\n");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline,
            "--allowlist", allowlist);

        Assert.Equal(1, code);
        Assert.Contains("unknown allowlistId: Foo", stderr, StringComparison.Ordinal);
        Assert.Contains("not-a-real-id", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_ExplicitScriptList_WaivesMismatch()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "RobotSelfDestructScript.psc"), TinyScript.Replace("Foo", "RobotSelfDestructScript", StringComparison.Ordinal));
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            """
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "RobotSelfDestructScript": { "sha256": "0000000000000000000000000000000000000000000000000000000000000000", "status": "identical" }
              }
            }
            """);
        var allowlist = Path.Combine(root.Path, "allowlist.md");
        File.WriteAllText(
            allowlist,
            """
            1. `parent-scope-params` — Parameter names in a parent scope.
               - scripts: RobotSelfDestructScript
            """);

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline,
            "--allowlist", allowlist);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("allowlisted: 1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_FlagsInCorpus_ChangeNormalizedHash()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var withoutFlags = CompileHashHarness.HashScript(TinyScript);
        File.WriteAllText(
            Path.Combine(root.Path, "TESV_Papyrus_Flags.flg"),
            "Flag Hidden 0\nFlag Conditional 1\n");
        var withFlags = Assert.Single(
            CompileHashHarness.CompilePath(root.Path, Papyrus.CodeAnalysis.PapyrusDialect.SkyrimSe));
        Assert.True(withFlags.Succeeded);
        Assert.NotEqual(withoutFlags, withFlags.Sha256);

        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(
            baseline,
            $$"""
            {
              "game": "skyrimse",
              "generated": "2026-08-26",
              "scripts": {
                "Foo": { "sha256": "{{withFlags.Sha256}}", "status": "identical" }
              }
            }
            """);

        var code = Run(
            out var stdout,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.Contains("identical: 1/1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_Compile_FlagsMissing_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript);
        var baseline = Path.Combine(root.Path, "synthetic.json");
        File.WriteAllText(baseline, """{ "game": "skyrimse", "generated": "2026-08-26", "scripts": {} }""");
        var missing = Path.Combine(root.Path, "nope.flg");

        var code = Run(
            out _,
            out var stderr,
            "verify",
            "--compile",
            "--game", "skyrimse",
            "--source", root.Path,
            "--baseline", baseline,
            "--flags", missing);

        Assert.Equal(1, code);
        Assert.Contains("flags file not found", stderr, StringComparison.Ordinal);
    }

    private static int Run(out string stdout, out string stderr, params string[] args)
    {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var code = Cli.Run(args, outWriter, errWriter);
        stdout = outWriter.ToString();
        stderr = errWriter.ToString();
        return code;
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrusc-verify-compile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
