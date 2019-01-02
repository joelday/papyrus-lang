#addin nuget:?package=Cake.Npm&version=0.16.0

var target = Argument("target", "default");
var solution = File("./DarkId.Papyrus.sln");

public void DownloadAndUnzip(string address, DirectoryPath outputPath)
{
    if (DirectoryExists(outputPath))
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

Task("download-compiler")
    .Does(() => {
        DownloadAndUnzip("https://www.dropbox.com/s/n03qh6ezt22q4qd/PapyrusCompiler.zip?dl=1", "./dependencies/compiler");
    });

Task("download-base-scripts")
    .Does(() => {
        DownloadAndUnzip("https://www.dropbox.com/s/xw646rnme3o30pu/Base.zip?dl=1", "./papyrus/FO4Scripts/Base");
    });

Task("restore")
    .Does(() => {
        DotNetCoreRestore();
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
        DotNetCoreVSTest("./src/DarkId.Papyrus.Test/bin/Debug/net461/DarkId.Papyrus.Test.dll");
    });

Task("clean")
    .Does(() => {
        CleanDirectories("./src/*/bin");
    });

Task("default")
    .IsDependentOn("clean")
    .IsDependentOn("download-compiler")
    .IsDependentOn("restore")
    .IsDependentOn("build")
    .IsDependentOn("download-base-scripts")
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

Task("build-test")
    .IsDependentOn("build")
    .IsDependentOn("test");

RunTarget(target);