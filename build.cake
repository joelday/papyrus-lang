#addin nuget:?package=Cake.Npm&version=0.16.0
#tool nuget:?package=Microsoft.TestPlatform&version=15.9.0

var target = Argument("target", "default");
var solution = File("./DarkId.Papyrus.sln");

var pluginFileDirectory = Directory("src/papyrus-lang-vscode/debug-plugin/");
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

Task("npm-semantic-release")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "semantic-release",
            WorkingDirectory = "src/papyrus-lang-vscode"
        });
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
        DotNetCoreVSTest("./src/DarkId.Papyrus.Test/bin/Debug/netcoreapp3.0/DarkId.Papyrus.Test.dll");
    });

Task("clean")
    .Does(() => {
        CleanDirectories("./src/*/bin");
    });

Task("default")
    .IsDependentOn("clean")
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