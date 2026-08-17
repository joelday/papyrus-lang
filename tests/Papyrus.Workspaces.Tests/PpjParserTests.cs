using System;
using System.IO;
using System.Text;
using System.Xml;
using Papyrus.CodeAnalysis;
using Papyrus.Pex;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class PpjParserTests
{
    [Fact]
    public void Parses_game_sse_and_import()
    {
        const string xml =
            """
            <?xml version="1.0"?>
            <PapyrusProject xmlns="PapyrusProject.xsd" Game="sse" Output="Scripts" Flags="TESV_Papyrus_Flags.flg">
              <Imports>
                <Import>.\Source\Scripts</Import>
              </Imports>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Equal(GameId.SkyrimSpecialEdition, project.Game);
        Assert.Same(PapyrusDialect.SkyrimSe, project.Dialect);
        Assert.Equal("Scripts", project.Output);
        Assert.Equal("TESV_Papyrus_Flags.flg", project.Flags);
        Assert.Equal([".\\Source\\Scripts"], project.Imports);
    }

    [Fact]
    public void Leaves_env_tokens_unexpanded()
    {
        const string xml =
            """
            <PapyrusProject Game="sse" Output="${OUTPUT_DIR}">
              <Imports>
                <Import>${SKYRIMSE_PATH}</Import>
              </Imports>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Equal("${OUTPUT_DIR}", project.Output);
        Assert.Equal(["${SKYRIMSE_PATH}"], project.Imports);
    }

    [Fact]
    public void Stores_https_import_paths_as_strings()
    {
        const string xml =
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>https://github.com/fireundubh/skyui/tree/master/dist/Data/Scripts/Source</Import>
              </Imports>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Equal(
            ["https://github.com/fireundubh/skyui/tree/master/dist/Data/Scripts/Source"],
            project.Imports);
    }

    [Fact]
    public void Parses_from_stream()
    {
        const string xml =
            """
            <PapyrusProject Game="fo4" Optimize="true" Release="true" Final="false" Asm="Discard">
              <Folders>
                <Folder NoRecurse="true">.\Source</Folder>
              </Folders>
              <Scripts>
                <Script>Foo.psc</Script>
              </Scripts>
            </PapyrusProject>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var project = PpjParser.Parse(stream);

        Assert.Equal(GameId.Fallout4, project.Game);
        Assert.Same(PapyrusDialect.Fallout4, project.Dialect);
        Assert.True(project.Optimize);
        Assert.True(project.Release);
        Assert.False(project.Final);
        Assert.Equal(AsmKind.Discard, project.Asm);
        Assert.Single(project.Folders);
        Assert.Equal(".\\Source", project.Folders[0].Path);
        Assert.True(project.Folders[0].NoRecurse);
        Assert.Equal(["Foo.psc"], project.Scripts);
    }

    [Theory]
    [InlineData("tesv", GameId.Skyrim)]
    [InlineData("tes5", GameId.Skyrim)]
    [InlineData("starfield", GameId.Starfield)]
    [InlineData("sf1", GameId.Starfield)]
    [InlineData("SSE", GameId.SkyrimSpecialEdition)]
    public void Maps_game_attribute(string game, GameId expected)
    {
        var project = PpjParser.Parse($"""<PapyrusProject Game="{game}" />""");
        Assert.Equal(expected, project.Game);
    }

    [Fact]
    public void Leaves_variable_values_unexpanded()
    {
        const string xml =
            """
            <PapyrusProject>
              <Variables>
                <Variable Name="ModName" Value="Demo"/>
                <Variable Name="ScriptsDir" Value="${GAME_DIR}\@ModName"/>
              </Variables>
              <Imports>
                <Import>@ScriptsDir</Import>
              </Imports>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Null(project.Game);
        Assert.Equal("Demo", project.Variables["ModName"]);
        Assert.Equal("${GAME_DIR}\\@ModName", project.Variables["ScriptsDir"]);
        Assert.Equal(["@ScriptsDir"], project.Imports);
    }

    [Fact]
    public void Prohibits_dtd()
    {
        const string xml =
            """
            <?xml version="1.0"?>
            <!DOCTYPE PapyrusProject [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <PapyrusProject Game="sse">
              <Imports>
                <Import>&xxe;</Import>
              </Imports>
            </PapyrusProject>
            """;

        Assert.Throws<XmlException>(() => PpjParser.Parse(xml));
    }

    [Fact]
    public void Rejects_non_project_root()
    {
        Assert.Throws<InvalidDataException>(() => PpjParser.Parse("<NotAProject />"));
    }
}
