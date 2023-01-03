#addin nuget:?package=Cake.Npm&version=2.0.0
#tool nuget:?package=Microsoft.TestPlatform&version=15.9.0
#tool nuget:?package=NuGet.CommandLine&version=5.9.1
#addin nuget:?package=Octokit&version=4.0.2
#addin nuget:?package=Cake.Git&version=2.0.0
// tool nuget:?package=GitVersion.CommandLine

var isCIBuild = EnvironmentVariable("CI") == "true";
var isRelease = isCIBuild && EnvironmentVariable("RELEASE") == "true";
var isPrerelease = isRelease && EnvironmentVariable("PRERELEASE") == "true";
var githubToken = EnvironmentVariable("CI") == "true" ? EnvironmentVariable("GH_TOKEN") ?? EnvironmentVariable("GITHUB_TOKEN") : null;
var branchName = EnvironmentVariable("CI") == "true" ? EnvironmentVariable("GITHUB_REF").Replace("refs/heads/", "") : null;

var target = Argument("target", "default");

var solution = File("./DarkId.Papyrus.sln");
var debuggerSolution = File("./DarkId.Papyrus.DebugServer.sln");

var forceDownloads = HasArgument("force-downloads");

var pluginFileDirectory = Directory("src/papyrus-lang-vscode/debug-plugin/");
var pyroCliDirectory = Directory("src/papyrus-lang-vscode/pyro/");

var version = "0.0.0";

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

    // TODO: Move debug server to the monorepo.
    var pluginDllZip = isPrerelease || !isCIBuild ?
        DownloadFile("https://github.com/joelday/papyrus-debug-server/releases/download/1.57.0-beta1/papyrus-debug-server.zip") :
        DownloadFile("https://github.com/joelday/papyrus-debug-server/releases/latest/download/papyrus-debug-server.zip");

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
    if (githubToken != null)
    {
        client.Credentials = new Octokit.Credentials(githubToken);
    }

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

// As much as the idea of a task with side effects grosses me out, meh...
Task("get-version")
    .Does(() => {
        Information("Getting version from semantic-release...");

        var done = false;

        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "generate-version-number",
            WorkingDirectory = "src/papyrus-lang-vscode",
            StandardOutputAction = (line) =>
            {
                Information("version stdout: " + line);

                if (done || !System.Version.TryParse(line, out var _))
                {
                    return;
                }

                done = true;
                version = line;
            }
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

Task("npm-copy-bin")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "copy-bin",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-copy-debug-bin")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "copy-debug-bin",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-clean")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "clean",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-build")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = isRelease ? "compile:release" : "compile",
            WorkingDirectory = "src/papyrus-lang-vscode",
        });
    });

Task("npm-publish")
    .Does(() => {
        NpmRunScript(new NpmRunScriptSettings()
        {
            ScriptName = "semantic-release",
            WorkingDirectory = "src/papyrus-lang-vscode",
            EnvironmentVariables = 
            {
                { "PRERELEASE_FLAG", isPrerelease ? "--pre-release" : null },
                { "VERSION", version },
                { "BRANCH_NAME", branchName }
            }
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
        var assemblyVersion = version + ".0";
        Information("Assembly version: " + assemblyVersion);

        MSBuild(debuggerSolution, new MSBuildSettings()
        {
            PlatformTarget = PlatformTarget.x64,
        });

        MSBuild(solution, new MSBuildSettings()
        {
            AssemblyVersion = assemblyVersion,
            Verbosity = Verbosity.Minimal
        });
    });

Task("test")
    .Does(() =>
    {
        var falloutTestTask = System.Threading.Tasks.Task.Run(() => VSTest("./src/DarkId.Papyrus.Test/bin/Debug/net472/DarkId.Papyrus.Test.Fallout4/DarkId.Papyrus.Test.Fallout4.dll", new VSTestSettings()
        {
            ToolPath = Context.Tools.Resolve("vstest.console.exe")
        }));

        var skyrimTestTask = System.Threading.Tasks.Task.Run(() => VSTest("./src/DarkId.Papyrus.Test/bin/Debug/net472/DarkId.Papyrus.Test.Skyrim/DarkId.Papyrus.Test.Skyrim.dll", new VSTestSettings()
        {
            ToolPath = Context.Tools.Resolve("vstest.console.exe")
        }));

        System.Threading.Tasks.Task.WaitAll(falloutTestTask, skyrimTestTask);
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

    if (isRelease)
    {
        builder
            .IsDependentOn("npm-ci")
            .IsDependentOn("get-version");
    }

    builder.IsDependentOn("clean")
        .IsDependentOn("download-compilers")
        .IsDependentOn("download-debug-plugin")
        .IsDependentOn("download-pyro-cli")
        .IsDependentOn("restore")
        .IsDependentOn("build")
        .IsDependentOn("test");

    if (isCIBuild && !builder.Task.Dependencies.Any(d => d.Name == "npm-ci"))
    {
        builder.IsDependentOn("npm-ci");
    }
    else
    {
        builder.IsDependentOn("npm-install");
    }

    builder
        .IsDependentOn("npm-clean")
        .IsDependentOn("npm-build")
        .IsDependentOn("npm-copy-bin")
        .IsDependentOn("npm-copy-debug-bin");

    if (isRelease)
    {
        builder.IsDependentOn("npm-publish");
    }
}

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