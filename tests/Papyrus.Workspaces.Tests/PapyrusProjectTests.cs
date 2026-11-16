using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Papyrus.Pex;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class PapyrusProjectTests : IDisposable
{
    private const string RemoteUrl = "https://github.com/MrOctopus/nl_online/tree/main/sse/source";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "papyrus-project-" + Guid.NewGuid().ToString("N"));

    public PapyrusProjectTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Source"));
        File.WriteAllText(Path.Combine(_root, "Source", "Mine.psc"), "ScriptName Mine\n");
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
    public void Without_pyro_a_remote_import_is_missing_and_nothing_is_fetched()
    {
        var project = PapyrusProject.Load(WriteProject(), pyro: null);

        Assert.Equal([RemoteUrl], project.MissingRemotes);
        Assert.DoesNotContain(project.Includes, include => include.Declared == RemoteUrl);
        Assert.Empty(project.Diagnostics);
    }

    [Fact]
    public void A_remote_import_in_pyros_cache_is_searched_after_the_project_folder()
    {
        var pyro = Path.Combine(_root, "pyro");
        Directory.CreateDirectory(pyro);
        File.WriteAllText(Path.Combine(pyro, "pyro.exe"), "");
        var hash = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(RemoteUrl)))[..8];
        var cached = Path.Combine(pyro, "remote", hash, "MrOctopus", "nl_online", "sse", "source");
        Directory.CreateDirectory(cached);
        File.WriteAllText(Path.Combine(cached, "Actor.psc"), "ScriptName Actor\n");

        var project = PapyrusProject.Load(WriteProject(), new PyroInstall(Path.Combine(pyro, "pyro.exe")));

        Assert.Empty(project.MissingRemotes);
        Assert.Equal(
            [Path.GetFullPath(_root), Path.GetFullPath(Path.Combine(_root, "Source")), Path.GetFullPath(cached)],
            project.Includes.Select(include => include.Directory));
        Assert.Equal([Path.GetFullPath(Path.Combine(_root, "Source", "Mine.psc"))], project.Scripts);
    }

    [Fact]
    public void Problems_in_the_file_are_reported_not_thrown()
    {
        var ppj = Path.Combine(_root, "broken.ppj");
        File.WriteAllText(
            ppj,
            """
            <PapyrusProject xmlns="PapyrusProject.xsd" Game="tes5" Output="@Missing">
              <Imports><Import>Source</Import><Import>NotThere</Import></Imports>
              <Folders><Folder>Source</Folder></Folders>
            </PapyrusProject>
            """);

        var project = PapyrusProject.Load(ppj);

        Assert.Equal(GameId.Skyrim, project.Game);
        Assert.Equal(["import-missing", "variable-unknown"], project.Diagnostics.Select(d => d.Code).Order());
        Assert.Equal([Path.GetFullPath(Path.Combine(_root, "Source", "Mine.psc"))], project.Scripts);
    }

    private string WriteProject()
    {
        var ppj = Path.Combine(_root, "mod.ppj");
        File.WriteAllText(
            ppj,
            $"""
            <PapyrusProject xmlns="PapyrusProject.xsd" Game="sse">
              <Imports>
                <Import>Source</Import>
                <Import>{RemoteUrl}</Import>
              </Imports>
              <Folders>
                <Folder>Source</Folder>
              </Folders>
            </PapyrusProject>
            """);
        return ppj;
    }
}
