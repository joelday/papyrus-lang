using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;

namespace Papyrus.LanguageServer.Tests;

/// <summary>
/// Shared fixture: a temp directory holding one sse project with a
/// <c>scripts/Foo.psc</c> script, loaded into a workspace behind a scheduler.
/// </summary>
internal sealed class LspTestWorkspace : IDisposable
{
    private LspTestWorkspace(
        string root,
        PapyrusWorkspace workspace,
        LspRequestScheduler scheduler,
        PapyrusProject project,
        string scriptPath)
    {
        Root = root;
        Workspace = workspace;
        Scheduler = scheduler;
        Project = project;
        ScriptPath = scriptPath;
    }

    internal string Root { get; }

    internal PapyrusWorkspace Workspace { get; }

    internal LspRequestScheduler Scheduler { get; }

    internal PapyrusProject Project { get; }

    internal string ScriptPath { get; }

    internal string ScriptUri => ToFileUri(ScriptPath);

    /// <summary>
    /// Writes an additional script directly to the project's scripts
    /// directory (no open document), for tests exercising cross-file
    /// features like references and rename.
    /// </summary>
    internal string WriteScript(string name, string text)
    {
        var path = Path.Combine(Path.GetDirectoryName(ScriptPath)!, name + ".psc");
        File.WriteAllText(path, text);
        return path;
    }

    internal static LspTestWorkspace Create(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        var scripts = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scripts);
        var scriptPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(scriptPath, "ScriptName Disk\n");
        var ppj = Path.Combine(root, "game.ppj");
        File.WriteAllText(
            ppj,
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>scripts</Import>
              </Imports>
            </PapyrusProject>
            """);

        var workspace = new PapyrusWorkspace(TimeProvider.System, enableFileWatching: false);
        var project = workspace.LoadProject(ppj);
        var scheduler = new LspRequestScheduler(workspace);
        return new LspTestWorkspace(root, workspace, scheduler, project, scriptPath);
    }

    internal async Task OpenAsync(string text, CancellationToken cancellationToken)
    {
        await Scheduler.ExecuteAsync(
            new DidOpenHandler(Workspace),
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = ScriptUri,
                    LanguageId = "papyrus",
                    Version = 1,
                    Text = text,
                },
            },
            cancellationToken);
    }

    internal static string ToFileUri(string path)
    {
        return new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Host = string.Empty,
            Path = Path.GetFullPath(path),
        }.Uri.AbsoluteUri;
    }

    public void Dispose()
    {
        Scheduler.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
