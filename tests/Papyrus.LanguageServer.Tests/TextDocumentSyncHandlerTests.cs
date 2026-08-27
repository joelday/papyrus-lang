using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis;
using Papyrus.LanguageServer;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class TextDocumentSyncHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "papyrus-lsp-sync-" + Guid.NewGuid().ToString("N"));

    public TextDocumentSyncHandlerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task DidOpen_change_close_update_open_documents_and_compilation_text()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var scripts = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(scripts);
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Disk\n");
        var ppj = Path.Combine(_root, "game.ppj");
        File.WriteAllText(
            ppj,
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>scripts</Import>
              </Imports>
            </PapyrusProject>
            """);

        var workspace = new PapyrusWorkspace(
            TimeProvider.System,
            enableFileWatching: false);
        var project = workspace.LoadProject(ppj);
        using var scheduler = new LspRequestScheduler(workspace);
        var didOpen = new DidOpenHandler(workspace);
        var didChange = new DidChangeHandler(workspace);
        var didClose = new DidCloseHandler(workspace);
        var snapshot = new SnapshotHandler();
        var uri = ToFileUri(diskPath);

        var before = await scheduler.ExecuteAsync(snapshot, parameters: null, timeout.Token);
        Assert.Equal(0, before.Generation);
        Assert.Equal(0, before.OpenDocumentCount);
        Assert.Equal(1, before.ProjectCount);

        await scheduler.ExecuteAsync(
            didOpen,
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = "papyrus",
                    Version = 1,
                    Text = "ScriptName Dirty\n",
                },
            },
            timeout.Token);

        var opened = await scheduler.ExecuteAsync(snapshot, parameters: null, timeout.Token);
        var documentId = DocumentId.Create(project.Id, diskPath);
        Assert.Equal(1, opened.Generation);
        Assert.Equal(1, opened.OpenDocumentCount);
        Assert.True(workspace.CurrentSolution.IsDocumentOpen(documentId));
        Assert.Equal("ScriptName Dirty\n", ReadScript(workspace, project.Id));

        await scheduler.ExecuteAsync(
            didChange,
            new DidChangeTextDocumentParams
            {
                TextDocument = new VersionedTextDocumentIdentifier { Uri = uri, Version = 2 },
                ContentChanges = [new TextDocumentContentChangeEvent { Text = "ScriptName Edited\n" }],
            },
            timeout.Token);

        var changed = await scheduler.ExecuteAsync(snapshot, parameters: null, timeout.Token);
        Assert.Equal(2, changed.Generation);
        Assert.Equal(1, changed.OpenDocumentCount);
        Assert.Equal("ScriptName Edited\n", ReadScript(workspace, project.Id));

        File.WriteAllText(diskPath, "ScriptName Disk2\n");
        await scheduler.ExecuteAsync(
            didClose,
            new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
            },
            timeout.Token);

        var closed = await scheduler.ExecuteAsync(snapshot, parameters: null, timeout.Token);
        Assert.Equal(3, closed.Generation);
        Assert.Equal(0, closed.OpenDocumentCount);
        Assert.False(workspace.CurrentSolution.IsDocumentOpen(documentId));
        Assert.Equal("ScriptName Disk2\n", ReadScript(workspace, project.Id));
    }

    [Fact]
    public async Task DidOpen_skips_projects_that_do_not_own_the_path()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var scripts = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(scripts);
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = Path.Combine(_root, "game.ppj");
        File.WriteAllText(
            ppj,
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>scripts</Import>
              </Imports>
            </PapyrusProject>
            """);

        var workspace = new PapyrusWorkspace(
            TimeProvider.System,
            enableFileWatching: false);
        workspace.LoadProject(ppj);
        using var scheduler = new LspRequestScheduler(workspace);
        var didOpen = new DidOpenHandler(workspace);

        await scheduler.ExecuteAsync(
            didOpen,
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = ToFileUri(Path.Combine(_root, "Other.psc")),
                    LanguageId = "papyrus",
                    Version = 1,
                    Text = "ScriptName Other\n",
                },
            },
            timeout.Token);

        Assert.Equal(1, scheduler.Generation);
        Assert.Empty(workspace.CurrentSolution.OpenDocumentIds);
    }

    private static string ToFileUri(string path)
    {
        return new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Host = string.Empty,
            Path = Path.GetFullPath(path),
        }.Uri.AbsoluteUri;
    }

    private static string ReadScript(PapyrusWorkspace workspace, ProjectId projectId)
    {
        return workspace.CurrentSolution
            .GetCompilation(projectId)
            .GetSyntaxTree(QualifiedName.Parse("Foo"))
            .GetText()
            .ToString();
    }
}
