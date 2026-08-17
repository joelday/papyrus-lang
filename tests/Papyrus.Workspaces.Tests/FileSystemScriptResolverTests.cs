using System;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.Pex;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class FileSystemScriptResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "papyrus-resolver-" + Guid.NewGuid().ToString("N"));

    public FileSystemScriptResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TryResolve_first_include_wins()
    {
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "Foo.psc"), "ScriptName First\n");
        File.WriteAllText(Path.Combine(second, "Foo.psc"), "ScriptName Second\n");

        var resolver = new FileSystemScriptResolver([first, second]);

        Assert.True(resolver.TryResolve(QualifiedName.Parse("Foo"), out var text, out var path));
        Assert.Equal("ScriptName First\n", text.ToString());
        Assert.Equal(Path.Combine(first, "Foo.psc"), path);
    }

    [Fact]
    public void TryResolve_namespaced_uses_forward_slash_relative_path()
    {
        var scripts = Path.Combine(_root, "scripts");
        var nested = Path.Combine(scripts, "A", "B");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Script.psc"), "ScriptName A:B:Script\n");

        var resolver = new FileSystemScriptResolver([scripts], game: GameId.Fallout4);

        Assert.True(resolver.TryResolve(QualifiedName.Parse("A:B:Script"), out var text, out var path));
        Assert.Equal("ScriptName A:B:Script\n", text.ToString());
        Assert.Equal(Path.Combine(nested, "Script.psc"), path);
    }

    [Fact]
    public void TryResolve_skyrim_does_not_treat_colon_as_path()
    {
        var scripts = Path.Combine(_root, "scripts");
        var nested = Path.Combine(scripts, "A", "B");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Script.psc"), "ScriptName Script\n");

        var resolver = new FileSystemScriptResolver([scripts], game: GameId.SkyrimSpecialEdition);

        Assert.False(resolver.TryResolve(QualifiedName.Parse("A:B:Script"), out _, out _));
    }

    [Fact]
    public void TryResolvePex_reads_pex_next_to_source()
    {
        var scripts = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(scripts);
        File.WriteAllBytes(Path.Combine(scripts, "Bar.pex"), WriteScriptPex("Bar"));

        var resolver = new FileSystemScriptResolver([scripts], game: GameId.SkyrimSpecialEdition);

        Assert.True(resolver.TryResolvePex("Bar", out var metadata));
        Assert.Equal("Bar", metadata.Name.Text);
    }

    [Fact]
    public void TryResolvePex_reads_pex_from_output_directory()
    {
        var scripts = Path.Combine(_root, "scripts");
        var output = Path.Combine(_root, "out");
        Directory.CreateDirectory(scripts);
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "OutScript.pex"), WriteScriptPex("OutScript"));

        var resolver = new FileSystemScriptResolver([scripts], output, GameId.SkyrimSpecialEdition);

        Assert.True(resolver.TryResolvePex("OutScript", out var metadata));
        Assert.Equal("OutScript", metadata.Name.Text);
    }

    [Fact]
    public void TryResolvePex_rejects_file_whose_object_name_does_not_match()
    {
        var scripts = Path.Combine(_root, "scripts");
        var output = Path.Combine(_root, "out");
        Directory.CreateDirectory(Path.Combine(scripts, "A"));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(scripts, "A", "Script.pex"), WriteScriptPex("Other"));
        File.WriteAllBytes(Path.Combine(output, "Script.pex"), WriteScriptPex("B:Script"));

        var resolver = new FileSystemScriptResolver([scripts], output, GameId.Fallout4);

        Assert.False(resolver.TryResolvePex("A:Script", out _));
        Assert.True(resolver.TryResolvePex("B:Script", out var metadata));
        Assert.Equal("B:Script", metadata.Name.Text);
    }

    [Fact]
    public void FromProject_uses_import_then_folder_order()
    {
        var import = Path.Combine(_root, "import");
        var folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(import);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(import, "Foo.psc"), "ScriptName Import\n");
        File.WriteAllText(Path.Combine(folder, "Foo.psc"), "ScriptName Folder\n");

        var project = PpjParser.Parse(
            $"""
            <PapyrusProject Game="sse" Output="out">
              <Imports>
                <Import>import</Import>
              </Imports>
              <Folders>
                <Folder>folder</Folder>
              </Folders>
            </PapyrusProject>
            """);

        var resolver = new FileSystemScriptResolver(project, _root);

        Assert.Equal(2, resolver.IncludeDirectories.Length);
        Assert.Equal(Path.GetFullPath(import), resolver.IncludeDirectories[0]);
        Assert.Equal(Path.GetFullPath(folder), resolver.IncludeDirectories[1]);
        Assert.True(resolver.TryResolve(QualifiedName.Parse("Foo"), out var text, out _));
        Assert.Equal("ScriptName Import\n", text.ToString());
    }

    [Fact]
    public void Compilation_can_bind_disk_source_and_pex()
    {
        var scripts = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(scripts);
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        File.WriteAllBytes(Path.Combine(scripts, "Bar.pex"), WriteScriptPex("Bar"));

        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new FileSystemScriptResolver([scripts], game: GameId.SkyrimSpecialEdition));

        Assert.Equal("Foo", compilation.GetScript(QualifiedName.Parse("Foo")).Name.Text);
        Assert.NotEmpty(compilation.GetScript(QualifiedName.Parse("Foo")).DeclaringSyntaxReferences);
        Assert.Equal("Bar", compilation.GetScript(QualifiedName.Parse("Bar")).Name.Text);
        Assert.Empty(compilation.GetScript(QualifiedName.Parse("Bar")).DeclaringSyntaxReferences);
    }

    private static byte[] WriteScriptPex(string name)
    {
        return PexWriter.Write(new PexFile
        {
            Version = PexVersion.V32,
            RawGameId = GameIdMapping.SkyrimRaw,
            Game = GameId.SkyrimSpecialEdition,
            Objects =
            [
                new PexObject
                {
                    Name = name,
                    ParentClassName = "",
                    AutoStateName = "",
                },
            ],
        });
    }
}
