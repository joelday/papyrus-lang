using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class WatchedProjectFileTests : IDisposable
{
    private const string Ppj = """
        <PapyrusProject Game="sse">
          <Imports>
            <Import>scripts</Import>
          </Imports>
        </PapyrusProject>
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "papyrus-lsp-watch-" + Guid.NewGuid().ToString("N"));
    private readonly PapyrusWorkspace _workspace = new(TimeProvider.System, enableFileWatching: false);
    private readonly LspRequestScheduler _scheduler;
    private readonly OpenBuffers _buffers = new();

    public WatchedProjectFileTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        _scheduler = new LspRequestScheduler(_workspace);
    }

    private string PpjPath => Path.Combine(_root, "game.ppj");

    private Task NotifyAsync(int type, CancellationToken ct) =>
        _scheduler.ExecuteAsync(
            new WatchedFilesHandler(_workspace, () => null, _buffers),
            new DidChangeWatchedFilesParams
            {
                Changes = [new FileEvent { Uri = LspTestWorkspace.ToFileUri(PpjPath), Type = type }],
            },
            ct);

    [Fact]
    public async Task A_project_created_later_loads_and_adopts_scripts_already_open()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var scriptPath = Path.Combine(_root, "scripts", "Foo.psc");
        File.WriteAllText(scriptPath, "ScriptName Foo\n");
        var uri = LspTestWorkspace.ToFileUri(scriptPath);
        await _scheduler.ExecuteAsync(
            new DidOpenHandler(_workspace, _buffers),
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = "papyrus",
                    Version = 1,
                    Text = "ScriptName Foo\nFunction Unsaved()\nEndFunction\n",
                },
            },
            timeout.Token);
        Assert.Empty(_workspace.CurrentSolution.ProjectIds);

        File.WriteAllText(PpjPath, Ppj);
        await NotifyAsync(1, timeout.Token);

        Assert.Single(_workspace.CurrentSolution.ProjectIds);
        var symbols = await _scheduler.ExecuteAsync(
            new DocumentSymbolHandler(),
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier { Uri = uri } },
            timeout.Token);
        var script = Assert.Single(symbols);
        Assert.Equal("Unsaved", Assert.Single(script.Children ?? []).Name);
    }

    [Fact]
    public async Task Changed_and_deleted_project_files_reload_and_unload()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        File.WriteAllText(PpjPath, Ppj);
        await NotifyAsync(1, timeout.Token);
        var id = Assert.Single(_workspace.CurrentSolution.ProjectIds);
        Assert.Equal("SkyrimSpecialEdition", _workspace.CurrentSolution.GetProject(id).Game.ToString());

        File.WriteAllText(PpjPath, Ppj.Replace("sse", "fo4", StringComparison.Ordinal));
        await NotifyAsync(2, timeout.Token);
        Assert.Equal("Fallout4", _workspace.CurrentSolution.GetProject(id).Game.ToString());

        File.Delete(PpjPath);
        await NotifyAsync(3, timeout.Token);
        Assert.Empty(_workspace.CurrentSolution.ProjectIds);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
