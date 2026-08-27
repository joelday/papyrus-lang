using System;
using System.IO;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.Compiler.Tests;

public sealed class CompileCommandTests
{
    [Fact]
    public void Help_MentionsCompile()
    {
        var code = Run(out var stdout, out var stderr, "--help");

        Assert.Equal(0, code);
        Assert.Contains("compile <script.psc", stdout, StringComparison.Ordinal);
        Assert.Contains("project.ppj", stdout, StringComparison.Ordinal);
        Assert.Contains("[--time <unix>]", stdout, StringComparison.Ordinal);
        Assert.Contains("[--game <", stdout, StringComparison.Ordinal);
        Assert.Contains("--game", stdout, StringComparison.Ordinal);
        Assert.Contains("skyrimle", stdout, StringComparison.Ordinal);
        Assert.Contains("--ignore-engine-limits", stdout, StringComparison.Ordinal);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Compile_MissingPath_ExitsNonZero()
    {
        var code = Run(out _, out var stderr, "compile", "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("path", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_MissingGame_ExitsNonZero()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());

        var code = Run(out _, out var stderr, "compile", script);

        Assert.Equal(1, code);
        Assert.Contains("--game must be one of", stderr, StringComparison.Ordinal);
        Assert.Contains("skyrimle", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_MissingFile_ExitsNonZero()
    {
        using var root = new TempDir();
        var missing = Path.Combine(root.Path, "nope.psc");

        var code = Run(out _, out var stderr, "compile", missing, "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("not found", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_TinyScript_WritesPexAndRoundTrips()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());
        var pexPath = Path.Combine(root.Path, "Foo.pex");

        var code = Run(out var stdout, out var stderr, "compile", script, "--game", "skyrimse");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);

        var bytes = File.ReadAllBytes(pexPath);
        var file = PexReader.Read(bytes);
        Assert.Equal(bytes, PexWriter.Write(file));
        Assert.Equal(PexVersion.V32, file.Version);
        Assert.Equal(GameId.SkyrimSpecialEdition, file.Game);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
        Assert.Equal("Foo", Assert.Single(file.Objects).Name);
    }

    [Fact]
    public void Compile_GameSkyrimLe_Accepted()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, TinyScript());
        var output = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(
            out _,
            out var stderr,
            "compile",
            script,
            "--game", "skyrimle",
            "--output", output,
            "--time", "0");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(output));

        var bytes = File.ReadAllBytes(output);
        var file = PexReader.Read(bytes, GameId.Skyrim);
        Assert.Equal(bytes, PexWriter.Write(file));
        Assert.Equal(PexVersion.V31, file.Version);
        Assert.Equal(GameId.Skyrim, file.Game);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
    }

    [Fact]
    public void Compile_ErrorDiagnostics_ExitsNonZeroAndDoesNotWrite()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, "ScriptName Foo extends MissingBase\nFunction Bar()\nEndFunction\n");
        var pexPath = Path.Combine(root.Path, "Foo.pex");
        var sentinel = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(pexPath, sentinel);

        var code = Run(out _, out var stderr, "compile", script, "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("PAP1001", stderr, StringComparison.Ordinal);
        Assert.Equal(sentinel, File.ReadAllBytes(pexPath));
    }

    [Fact]
    public void Compile_Ppj_TinyProject_WritesPex()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript());
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="sse" Output="out">
              <Imports>
                <Import>.</Import>
              </Imports>
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);
        var pexPath = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(out var stdout, out var stderr, "compile", project);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);

        var bytes = File.ReadAllBytes(pexPath);
        var file = PexReader.Read(bytes);
        Assert.Equal(bytes, PexWriter.Write(file));
        Assert.Equal(PexVersion.V32, file.Version);
        Assert.Equal(GameId.SkyrimSpecialEdition, file.Game);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
        Assert.Equal("Foo", Assert.Single(file.Objects).Name);
    }

    [Fact]
    public void Compile_Ppj_CliGame_OverridesProjectGame()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript());
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="fo4" Output="out">
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);
        var pexPath = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(out _, out var stderr, "compile", project, "--game", "skyrimse");

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));

        var file = PexReader.Read(File.ReadAllBytes(pexPath));
        Assert.Equal(PexVersion.V32, file.Version);
        Assert.Equal(GameId.SkyrimSpecialEdition, file.Game);
    }

    [Fact]
    public void Compile_Ppj_Folder_WritesPex()
    {
        using var root = new TempDir();
        var scripts = Path.Combine(root.Path, "scripts");
        Directory.CreateDirectory(scripts);
        Directory.CreateDirectory(Path.Combine(scripts, "nested"));
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), TinyScript());
        File.WriteAllText(Path.Combine(scripts, "nested", "Bar.psc"), "ScriptName Bar\nFunction Baz()\nEndFunction\n");
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="sse" Output="out">
              <Folders>
                <Folder NoRecurse="true">scripts</Folder>
              </Folders>
            </PapyrusProject>
            """);
        var pexPath = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(out var stdout, out var stderr, "compile", project);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root.Path, "out", "Bar.pex")));
        Assert.False(File.Exists(Path.Combine(root.Path, "out", "nested", "Bar.pex")));
    }

    [Fact]
    public void Compile_Ppj_ScriptError_SkipsBrokenPex()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript());
        File.WriteAllText(
            Path.Combine(root.Path, "Bar.psc"),
            "ScriptName Bar extends MissingBase\nFunction Baz()\nEndFunction\n");
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="sse" Output="out">
              <Scripts>
                <Script>Foo.psc</Script>
                <Script>Bar.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);
        var fooPex = Path.Combine(root.Path, "out", "Foo.pex");
        var barPex = Path.Combine(root.Path, "out", "Bar.pex");

        var code = Run(out var stdout, out var stderr, "compile", project);

        Assert.Equal(1, code);
        Assert.Contains("PAP1001", stderr, StringComparison.Ordinal);
        Assert.True(File.Exists(fooPex));
        Assert.Contains(fooPex, stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(barPex));
    }

    [Fact]
    public void Compile_Ppj_IgnoreEngineLimits_WritesPex()
    {
        using var root = new TempDir();
        File.WriteAllText(
            Path.Combine(root.Path, "Foo.psc"),
            "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[129]\nEndFunction\n");
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="sse" Output="out">
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);
        var pexPath = Path.Combine(root.Path, "out", "Foo.pex");

        var blocked = Run(out _, out var blockedErr, "compile", project);
        Assert.Equal(1, blocked);
        Assert.Contains("PAP2002", blockedErr, StringComparison.Ordinal);
        Assert.False(File.Exists(pexPath));

        var code = Run(out var stdout, out var stderr, "compile", project, "--ignore-engine-limits");

        Assert.Equal(0, code);
        Assert.Contains("warning PAP2002", stderr, StringComparison.Ordinal);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Ppj_ScriptInImport_WritesPex()
    {
        using var root = new TempDir();
        var source = Path.Combine(root.Path, "Source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Foo.psc"), TinyScript());
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            """
            <PapyrusProject Game="sse" Output="out">
              <Imports>
                <Import>Source</Import>
              </Imports>
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);
        var pexPath = Path.Combine(root.Path, "out", "Foo.pex");

        var code = Run(out var stdout, out var stderr, "compile", project);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Ppj_Malformed_ExitsNonZero()
    {
        using var root = new TempDir();
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(project, "<NotAProject />");
        var pexPath = Path.Combine(root.Path, "Mod.pex");

        var code = Run(out _, out var stderr, "compile", project, "--game", "skyrimse");

        Assert.Equal(1, code);
        Assert.Contains("error:", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(pexPath));
    }

    [Fact]
    public void Compile_Ppj_ExpandsEnvInImportAndOutput()
    {
        using var root = new TempDir();
        var importDir = Path.Combine(root.Path, "import");
        var outputDir = Path.Combine(root.Path, "built");
        Directory.CreateDirectory(importDir);
        File.WriteAllText(Path.Combine(importDir, "Foo.psc"), TinyScript());
        var importVar = "PAPYRUS_TEST_IMPORT_" + Guid.NewGuid().ToString("N");
        var outputVar = "PAPYRUS_TEST_OUTPUT_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(importVar, importDir);
        Environment.SetEnvironmentVariable(outputVar, outputDir);
        try
        {
            var project = Path.Combine(root.Path, "Mod.ppj");
            File.WriteAllText(
                project,
                $$"""
                <PapyrusProject Game="sse" Output="${{{outputVar}}}">
                  <Imports>
                    <Import>${{{importVar}}}</Import>
                  </Imports>
                  <Scripts>
                    <Script>Foo.psc</Script>
                  </Scripts>
                </PapyrusProject>
                """);
            var pexPath = Path.Combine(outputDir, "Foo.pex");

            var code = Run(out var stdout, out var stderr, "compile", project);

            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.True(File.Exists(pexPath));
            Assert.Contains(pexPath, stdout, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(importVar, null);
            Environment.SetEnvironmentVariable(outputVar, null);
        }
    }

    [Fact]
    public void Compile_Ppj_MissingEnvVar_ExitsNonZero()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript());
        var missing = "PAPYRUS_TEST_MISSING_" + Guid.NewGuid().ToString("N");
        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            $$"""
            <PapyrusProject Game="sse" Output="${{{missing}}}">
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """);

        var code = Run(out _, out var stderr, "compile", project);

        Assert.Equal(1, code);
        Assert.Contains(missing, stderr, StringComparison.Ordinal);
        Assert.Contains("not set", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root.Path, "Foo.pex")));
    }

    [Fact]
    public void Compile_Ppj_PreAndPostBuild_WriteMarkers()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "Foo.psc"), TinyScript());
        string preCommand;
        string postCommand;
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(root.Path, "pre.cmd"), "echo. > pre.marker\r\n");
            File.WriteAllText(Path.Combine(root.Path, "post.cmd"), "echo. > post.marker\r\n");
            preCommand = "pre.cmd";
            postCommand = "post.cmd";
        }
        else
        {
            File.WriteAllText(Path.Combine(root.Path, "pre.sh"), "echo pre > pre.marker\n");
            File.WriteAllText(Path.Combine(root.Path, "post.sh"), "echo post > post.marker\n");
            preCommand = "sh pre.sh";
            postCommand = "sh post.sh";
        }

        var project = Path.Combine(root.Path, "Mod.ppj");
        File.WriteAllText(
            project,
            $"""
            <PapyrusProject Game="sse" Output="out">
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
              <PreBuild>
                <Command>{preCommand}</Command>
              </PreBuild>
              <PostBuild>
                <Command>{postCommand}</Command>
              </PostBuild>
            </PapyrusProject>
            """);

        var code = Run(out var stdout, out var stderr, "compile", project);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(root.Path, "out", "Foo.pex")));
        Assert.Contains(Path.Combine(root.Path, "out", "Foo.pex"), stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root.Path, "pre.marker")));
        Assert.True(File.Exists(Path.Combine(root.Path, "post.marker")));
        Assert.DoesNotContain("error:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_IgnoreEngineLimits_WritesPex()
    {
        using var root = new TempDir();
        var script = Path.Combine(root.Path, "Foo.psc");
        File.WriteAllText(script, "ScriptName Foo\nFunction Bar()\n  Int[] a = new Int[129]\nEndFunction\n");
        var pexPath = Path.Combine(root.Path, "Foo.pex");

        var blocked = Run(out _, out var blockedErr, "compile", script, "--game", "skyrimse");
        Assert.Equal(1, blocked);
        Assert.Contains("PAP2002", blockedErr, StringComparison.Ordinal);
        Assert.False(File.Exists(pexPath));

        var code = Run(out var stdout, out var stderr, "compile", script, "--game", "skyrimse", "--ignore-engine-limits");

        Assert.Equal(0, code);
        Assert.Contains("warning PAP2002", stderr, StringComparison.Ordinal);
        Assert.True(File.Exists(pexPath));
        Assert.Contains(pexPath, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_SameNameImport_EmitsSpecifiedFile()
    {
        using var root = new TempDir();
        var import = Path.Combine(root.Path, "import");
        var src = Path.Combine(root.Path, "src");
        Directory.CreateDirectory(Path.Combine(import, "NS"));
        Directory.CreateDirectory(Path.Combine(src, "NS"));
        File.WriteAllText(
            Path.Combine(import, "NS", "Foo.psc"),
            "ScriptName NS:Foo\nInt Function Marker()\n  Return 7\nEndFunction\n");
        var script = Path.Combine(src, "NS", "Foo.psc");
        File.WriteAllText(
            script,
            "ScriptName NS:Foo\nInt Function Marker()\n  Return 42\nEndFunction\n");
        var pexPath = Path.Combine(src, "NS", "Foo.pex");

        var code = Run(
            out _,
            out var stderr,
            "compile",
            script,
            "--game", "fallout4",
            "--import", import,
            "--import", src);

        Assert.Equal(0, code);
        Assert.Empty(stderr);
        Assert.True(File.Exists(pexPath));

        var file = PexReader.Read(File.ReadAllBytes(pexPath));
        Assert.Equal("NS:Foo", Assert.Single(file.Objects).Name);
        var marker = Function(file, "Marker");
        Assert.Contains(
            marker.Instructions,
            static i => i.OpCode == PexOpCode.Return
                && i.Arguments.Length > 0
                && i.Arguments[0].Type == PexValueType.Integer
                && i.Arguments[0].Integer == 42);
        Assert.DoesNotContain(
            marker.Instructions,
            static i => i.OpCode == PexOpCode.Return
                && i.Arguments.Length > 0
                && i.Arguments[0].Type == PexValueType.Integer
                && i.Arguments[0].Integer == 7);
    }

    private static PexFunction Function(PexFile file, string name)
    {
        var obj = Assert.Single(file.Objects);
        foreach (var state in obj.States)
        {
            foreach (var function in state.Functions)
            {
                if (string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return function;
                }
            }
        }

        throw new InvalidOperationException("Function '" + name + "' was not emitted.");
    }

    private static string TinyScript()
    {
        return "ScriptName Foo\nFunction Bar()\nEndFunction\n";
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "papyrusc-compile-" + Guid.NewGuid().ToString("N"));
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
