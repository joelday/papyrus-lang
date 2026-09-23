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

    [Theory]
    [InlineData("Institute_Papyrus_Flags.flg", GameId.Fallout4)]
    [InlineData("TESV_Papyrus_Flags.flg", GameId.SkyrimSpecialEdition)]
    [InlineData(@"C:\Games\Fallout 4\Data\Scripts\Source\Base\institute_papyrus_flags.flg", GameId.Fallout4)]
    [InlineData("../flags/TESV_Papyrus_Flags.flg", GameId.SkyrimSpecialEdition)]
    public void Infers_game_from_flags_when_game_attribute_is_absent(string flags, GameId expected)
    {
        var project = PpjParser.Parse($"""<PapyrusProject Flags="{flags}" />""");
        Assert.Equal(expected, project.Game);
    }

    [Fact]
    public void Game_attribute_wins_over_flags_inference()
    {
        var project = PpjParser.Parse("""<PapyrusProject Game="tesv" Flags="TESV_Papyrus_Flags.flg" />""");
        Assert.Equal(GameId.Skyrim, project.Game);
    }

    [Theory]
    [InlineData("Custom_Flags.flg")]
    [InlineData("@FlagsFile")]
    public void Leaves_game_unset_for_unrecognized_flags(string flags)
    {
        var project = PpjParser.Parse($"""<PapyrusProject Flags="{flags}" />""");
        Assert.Null(project.Game);
    }

    [Fact]
    public void Parses_pre_and_post_build_commands()
    {
        const string xml =
            """
            <PapyrusProject Game="sse">
              <PreBuild>
                <Command>echo pre</Command>
              </PreBuild>
              <PostBuild>
                <Command>echo post</Command>
                <Command>echo post2</Command>
              </PostBuild>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Equal(["echo pre"], project.PreBuild);
        Assert.Equal(["echo post", "echo post2"], project.PostBuild);
    }

    [Fact]
    public void Parses_pyro_pre_and_post_build_event_aliases()
    {
        const string xml =
            """
            <PapyrusProject Game="sse">
              <PreBuildEvent>
                <Command>echo pre</Command>
              </PreBuildEvent>
              <PostBuildEvent>
                <Command>echo post</Command>
              </PostBuildEvent>
            </PapyrusProject>
            """;

        var project = PpjParser.Parse(xml);

        Assert.Equal(["echo pre"], project.PreBuild);
        Assert.Equal(["echo post"], project.PostBuild);
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

        Assert.True(project.TryExpandEnvironmentVariables(out var expanded, out var missing));
        Assert.Equal("", missing);
        Assert.Equal("${GAME_DIR}\\@ModName", expanded.Variables["ScriptsDir"]);
        Assert.Equal(["@ScriptsDir"], expanded.Imports);
    }

    [Fact]
    public void ExpandEnvironmentVariables_expands_non_identifier_names()
    {
        var name = "PAPYRUS_TEST_DIR(x86)_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "expanded-out");
        try
        {
            var project = PpjParser.Parse(
                $$"""
                <PapyrusProject Game="sse" Output="${{{name}}}">
                  <Imports>
                    <Import>${{{name}}}/scripts</Import>
                  </Imports>
                </PapyrusProject>
                """);

            Assert.True(project.TryExpandEnvironmentVariables(out var expanded, out var missing));
            Assert.Equal("", missing);
            Assert.Equal("expanded-out", expanded.Output);
            Assert.Equal(["expanded-out/scripts"], expanded.Imports);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
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
