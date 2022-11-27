#addin nuget:?package=Cake.Npm&version=2.0.0
#tool nuget:?package=Microsoft.TestPlatform&version=15.9.0
#tool nuget:?package=NuGet.CommandLine&version=5.9.1
#addin nuget:?package=Octokit&version=4.0.2
#addin nuget:?package=Cake.Git&version=2.0.0

var isCIBuild = EnvironmentVariable("CI") == "true";
var isRelease = isCIBuild && EnvironmentVariable("RELEASE") == "true";
var isPrerelease = isRelease && EnvironmentVariable("PRERELEASE") == "true";

var target = Argument("target", "default");
var solution = File("./DarkId.Papyrus.sln");
var forceDownloads = HasArgument("force-downloads");

var pluginFileDirectory = Directory("src/papyrus-lang-vscode/debug-plugin/");
var pyroCliDirectory = Directory("src/papyrus-lang-vscode/pyro/");
var currentVersion = GitVersion();

public bool ShouldContinueWithDownload(DirectoryPath path)
{
    if (DirectoryExists(path))
    {
        if (forceDownloads)
        {
            DeleteDirectory(path, new DeleteDirectorySettings { Recursive = true, Force = true });
            return true;
        }
        else
        {
            return false;
        }
    }

    return true;
}

public void UpdateDebugPlugin()
{
    if (!ShouldContinueWithDownload(pluginFileDirectory))
    {
        return;
    }

    var pluginDllZip = DownloadFile("https://github.com/joelday/papyrus-debug-server/releases/latest/download/papyrus-debug-server.zip");
    Unzip(pluginDllZip, pluginFileDirectory);

    Information("Debug plugin update complete.");
}

public void UpdatePyroCli()
{
    if (!ShouldContinueWithDownload(pyroCliDirectory))
    {
        return;
    }

    var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("Papyrus-Lang-CI"));

    client.Repository.Release.GetAll("fireundubh", "pyro").ContinueWith((task) =>
    {
        var latestRelease = task.Result.First();

        Information("Found latest release: " + latestRelease.Name);

        var latestReleaseAsset = latestRelease.Assets.First();
        var downloadUrl = latestReleaseAsset.BrowserDownloadUrl;

        var pyroCliZip = DownloadFile(downloadUrl);
        Unzip(pyroCliZip, pyroCliDirectory);

        Information("Pyro update complete.");
    }).Wait();
}

public void DownloadCompilers() {
    if (!ShouldContinueWithDownload(Directory("dependencies/compilers")))
    {
        return;
    }

    var filePath = DownloadFile("https://www.dropbox.com/s/vkoffvsdhru7p1c/papyrus-compilers.zip?dl=1");
    Unzip(filePath, "./dependencies");
}

public void NpmScript(string scriptName)
{
    var settings = new NpmRunScriptSettings()
    {
        ScriptName = scriptName,
        WorkingDirectory = "./src/papyrus-lang-vscode"
    };

    NpmRunScript(settings);
}

foreach (var scriptName in new string[]
    {
        "copy-bin",
        "copy-debug-bin",
        "clean",
        "changelog:update"
    })
{
    Task($"npm-{scriptName}")
        .Does(() => NpmScript(scriptName));
}

Task("npm-version")
    .Does(() =>
    {
        NpmVersion(new NpmVersionSettings()
        {
            WorkingDirectory = "./src/papyrus-lang-vscode",
            ArgumentCustomization = args => args
                .Append(currentVersion.SemVer)
                .Append("--allow-same-version")
                .Append("--no-git-tag-version")
                .Append("--no-commit-hooks")
        });
    });

Task("npm-install")
    .Does(() => {
        NpmInstall(new NpmInstallSettings()
        {
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-ci")
    .Does(() => {
        NpmCi(new NpmCiSettings()
        {
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-build")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = isCIBuild ? "compile:release" : "compile",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-publish")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = isPrerelease ? "publish" : "publish:prerelease",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("download-compilers")
    .Does(() => {
        DownloadCompilers();
    });

Task("download-debug-plugin")
    .Does(() => {
        UpdateDebugPlugin();
    });

Task("download-pyro-cli")
    .Does(() => {
        UpdatePyroCli();
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
            AssemblyVersion = currentVersion.AssemblySemVer,
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
        CleanDirectories("./src/*/bin-debug");
        CleanDirectories("./src/*/obj");
    });

void BuildDefaultTask()
{
    var builder = Task("default");

    builder.IsDependentOn("clean")
        .IsDependentOn("download-compilers")
        .IsDependentOn("download-debug-plugin")
        .IsDependentOn("download-pyro-cli")
        .IsDependentOn("restore")
        .IsDependentOn("build")
        .IsDependentOn("test");

    if (isRelease)
    {
        builder.IsDependentOn("npm-version");
    }

    if (isCIBuild)
    {
        builder.IsDependentOn("npm-ci");
    }
    else
    {
        builder.IsDependentOn("npm-install");
    }

    builder.IsDependentOn("npm-build")
        .IsDependentOn("npm-copy-bin")
        .IsDependentOn("npm-copy-debug-bin")
        .IsDependentOn("npm-clean");
}

Task("publish")
    .IsDependentOn("npm-changelog:update")
    .IsDependentOn("npm-publish");

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

BuildDefaultTask();
RunTarget(target);