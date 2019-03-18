#addin nuget:?package=Cake.Npm&version=0.16.0
#tool nuget:?package=Microsoft.TestPlatform&version=15.9.0

var target = Argument("target", "default");
var solution = File("./DarkId.Papyrus.sln");

public void DownloadAndUnzip(string address, DirectoryPath outputPath, DirectoryPath existsPath)
{
    if (DirectoryExists(existsPath))
    {
        return;
    }

    var filePath = DownloadFile(address);
    Unzip(filePath, outputPath);
}

Task("npm-install")
    .Does(() => {
        NpmInstall(new NpmInstallSettings()
        {
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });

Task("npm-clean")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "clean",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });

Task("npm-copy-bin")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "copy-bin",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });


Task("npm-build")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "compile",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });

Task("download-compilers")
    .Does(() => {
        DownloadAndUnzip("https://www.dropbox.com/s/vkoffvsdhru7p1c/papyrus-compilers.zip?dl=1", "./dependencies", "./dependencies/compilers");
    });

Task("restore")
    .Does(() => {
        NuGetRestore(solution);
    });

Task("build")
    .Does(() =>
    {
        MSBuild(solution, new MSBuildSettings()
        {
            Verbosity = Verbosity.Minimal
        });
    });

Task("test")
    .Does(() =>
    {
        VSTest("./src/DarkId.Papyrus.Test/bin/Debug/net461/DarkId.Papyrus.Test.Fallout4/DarkId.Papyrus.Test.Fallout4.dll", new VSTestSettings()
        {
            ToolPath = Context.Tools.Resolve("vstest.console.exe")
        });

        VSTest("./src/DarkId.Papyrus.Test/bin/Debug/net461/DarkId.Papyrus.Test.Skyrim/DarkId.Papyrus.Test.Skyrim.dll", new VSTestSettings()
        {
            ToolPath = Context.Tools.Resolve("vstest.console.exe")
        });
    });

Task("clean")
    .Does(() => {
        CleanDirectories("./src/*/bin");
    });

Task("default")
    .IsDependentOn("clean")
    .IsDependentOn("download-compilers")
    .IsDependentOn("restore")
    .IsDependentOn("build")
    .IsDependentOn("test")
    .IsDependentOn("npm-install")
    .IsDependentOn("npm-clean")
    .IsDependentOn("npm-copy-bin")
    .IsDependentOn("npm-build");

Task("update-bin")
    .IsDependentOn("build")
    .IsDependentOn("npm-copy-bin");

Task("build-extension")
    .IsDependentOn("npm-clean")
    .IsDependentOn("npm-copy-bin")
    .IsDependentOn("npm-build");

Task("ci-build")
    .IsDependentOn("download-compilers")
    .IsDependentOn("restore")
    .IsDependentOn("build");

RunTarget(target);