#addin nuget:?package=Cake.Npm&version=0.16.0
#tool nuget:?package=Microsoft.TestPlatform&version=15.9.0

var target = Argument("target", "default");
var solution = File("./DarkId.Papyrus.sln");

var pluginFileDirectory = Directory("src/papyrus-lang-vscode/debug-plugin/");
var pyroCliDirectory = Directory("src/papyrus-lang-vscode/pyro/");

var isCIBuild = EnvironmentVariable("APPVEYOR") == "true";

public void DownloadAndUnzip(string address, DirectoryPath outputPath, DirectoryPath existsPath)
{
    if (DirectoryExists(existsPath))
    {
        return;
    }

    var filePath = DownloadFile(address);
    Unzip(filePath, outputPath);
}

public void UpdateDebugPlugin()
{
    if (DirectoryExists(pluginFileDirectory))
    {
        DeleteDirectory(pluginFileDirectory, new DeleteDirectorySettings()
        {
            Recursive = true,
            Force = true
        });
    }
    var pluginDllZip = DownloadFile("https://github.com/joelday/papyrus-debug-server/releases/latest/download/papyrus-debug-server.zip");
    Unzip(pluginDllZip, pluginFileDirectory);
    Information("Debug plugin update complete.");

}

public void UpdatePyroCli()
{
    if (DirectoryExists(pyroCliDirectory))
    {
        DeleteDirectory(pyroCliDirectory, new DeleteDirectorySettings()
        {
            Recursive = true,
            Force = true
        });
    }
    var pyroCliZip = DownloadFile("https://github.com/fireundubh/pyro/releases/download/2019-11-22/pyro.zip");
    Unzip(pyroCliZip, pyroCliDirectory + Directory(".."));
    Information("Pyro update complete.");
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

Task("npm-copy-debug-bin")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "copy-debug-bin",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });

Task("npm-build")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = isCIBuild ? "compile:release" : "compile",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
    });

Task("update-debug-plugin")
    .Does(() => {
        UpdateDebugPlugin();
    });

Task("update-pyro-cli")
    .Does(() => {
        UpdatePyroCli();
    });

Task("npm-semantic-release")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "semantic-release",
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
    .IsDependentOn("npm-copy-debug-bin")
    .IsDependentOn("npm-build");

Task("update-bin")
    .IsDependentOn("build")
    .IsDependentOn("npm-copy-bin")
    .IsDependentOn("npm-copy-debug-bin");

Task("build-extension")
    .IsDependentOn("npm-clean")
    .IsDependentOn("npm-copy-bin")
    .IsDependentOn("npm-copy-debug-bin")
    .IsDependentOn("npm-build");

Task("build-test")
    .IsDependentOn("build")
    .IsDependentOn("test");

RunTarget(target);