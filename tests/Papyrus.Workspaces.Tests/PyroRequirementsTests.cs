using System;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class PyroRequirementsTests
{
    [Fact]
    public void Plain_project_needs_nothing()
    {
        var project = PpjParser.Parse(
            """
            <PapyrusProject xmlns="PapyrusProject.xsd" Game="sse" Flags="TESV_Papyrus_Flags.flg" Output="Scripts">
              <Imports><Import>.\Source</Import><Import>C:\Games\Skyrim\Data\Scripts\Source</Import></Imports>
              <Scripts><Script>Foo.psc</Script></Scripts>
              <PostBuildEvent><Command>notify user@example.com</Command></PostBuildEvent>
            </PapyrusProject>
            """);

        Assert.Empty(PyroRequirements.Analyze(project));
    }

    [Fact]
    public void Pyro_only_content_remote_imports_and_variables_are_reported()
    {
        var project = PpjParser.Parse(
            """
            <PapyrusProject xmlns="PapyrusProject.xsd" Game="fo4" Package="true" Output="@OutDir">
              <Variables><Variable Name="OutDir" Value="Scripts"/></Variables>
              <Imports>
                <Import>https://github.com/fireundubh/skyui/tree/master/dist/Data/Scripts/Source</Import>
                <Import>@ModRoot\Source</Import>
              </Imports>
              <Packages><Package Name="Mod" RootDir="Scripts"/></Packages>
            </PapyrusProject>
            """);

        var reasons = PyroRequirements.Analyze(project);

        Assert.Equal(3, reasons.Length);
        Assert.Contains("Package attribute", reasons[0], StringComparison.Ordinal);
        Assert.Contains("<Packages>", reasons[0], StringComparison.Ordinal);
        Assert.Contains("https://github.com/fireundubh/skyui", reasons[1], StringComparison.Ordinal);
        Assert.Contains("@OutDir", reasons[2], StringComparison.Ordinal);
        Assert.Contains("@ModRoot", reasons[2], StringComparison.Ordinal);
    }
}
