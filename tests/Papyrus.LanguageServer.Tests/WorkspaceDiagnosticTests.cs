using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class WorkspaceDiagnosticTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "papyrus-lsp-wsdiag-" + Guid.NewGuid().ToString("N"));
    private readonly PapyrusWorkspace _workspace = new(TimeProvider.System, enableFileWatching: false);
    private readonly LspRequestScheduler _scheduler;

    public WorkspaceDiagnosticTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, "base"));
        File.WriteAllText(Path.Combine(_root, "src", "Good.psc"), "ScriptName Good\n");
        File.WriteAllText(Path.Combine(_root, "src", "Bad.psc"), "ScriptName Bad\nMissingType x\n");
        File.WriteAllText(Path.Combine(_root, "src", "Open.psc"), "ScriptName Open\n");
        // An import: compiled against, never reported.
        File.WriteAllText(Path.Combine(_root, "base", "Vanilla.psc"), "ScriptName Vanilla\nAlsoMissing y\n");
        File.WriteAllText(
            Path.Combine(_root, "game.ppj"),
            """
            <PapyrusProject Game="sse">
              <Imports>
                <Import>src</Import>
                <Import>base</Import>
              </Imports>
              <Folders>
                <Folder>src</Folder>
              </Folders>
            </PapyrusProject>
            """);
        _workspace.LoadProject(Path.Combine(_root, "game.ppj"));
        _scheduler = new LspRequestScheduler(_workspace);
    }

    private string Uri(params string[] parts) => LspTestWorkspace.ToFileUri(Path.Combine([_root, .. parts]));

    [Fact]
    public async Task Reports_the_projects_own_unopened_scripts_and_marks_unchanged_ones()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await _scheduler.ExecuteAsync(
            new DidOpenHandler(_workspace),
            new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem { Uri = Uri("src", "Open.psc"), LanguageId = "papyrus", Version = 1, Text = "ScriptName Open\n" },
            },
            timeout.Token);

        var first = await _scheduler.ExecuteAsync(new WorkspaceDiagnosticHandler(), new WorkspaceDiagnosticParams(), timeout.Token);

        Assert.Equal(
            ["Bad.psc", "Good.psc"],
            first.Items.Select(i => Path.GetFileName(new Uri(i.Uri).LocalPath)).Order().ToArray());
        Assert.All(first.Items, i => Assert.Equal("full", i.Kind));
        var bad = Assert.Single(first.Items, i => i.Uri.EndsWith("Bad.psc", StringComparison.Ordinal));
        Assert.NotEmpty(bad.Items!);

        var second = await _scheduler.ExecuteAsync(
            new WorkspaceDiagnosticHandler(),
            new WorkspaceDiagnosticParams
            {
                PreviousResultIds = first.Items.Select(i => new PreviousResultId { Uri = i.Uri, Value = i.ResultId! }).ToArray(),
            },
            timeout.Token);

        Assert.All(second.Items, i => Assert.Equal("unchanged", i.Kind));
        Assert.All(second.Items, i => Assert.Null(i.Items));
    }

    [Fact]
    public void Source_documents_are_the_folders_scripts_not_the_imports()
    {
        var project = Assert.Single(_workspace.CurrentSolution.ProjectIds);

        var sources = _workspace.CurrentSolution.GetSourceDocumentIds(project);

        Assert.Equal(["Bad.psc", "Good.psc", "Open.psc"], sources.Select(d => Path.GetFileName(d.Path)).Order().ToArray());
    }

    [Fact]
    public async Task Poller_holds_an_answered_request_until_something_changes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var poller = new WorkspaceDiagnosticsPoller(TimeSpan.FromMilliseconds(10));
        var calls = 0;
        Task<WorkspaceDiagnosticReport> Compute(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new WorkspaceDiagnosticReport());
        }

        var repoll = new WorkspaceDiagnosticParams { PreviousResultIds = [new PreviousResultId { Uri = "file:///x", Value = "1" }] };
        await poller.PollAsync(new WorkspaceDiagnosticParams(), Compute, timeout.Token);
        var held = poller.PollAsync(repoll, Compute, timeout.Token);
        await Task.Delay(100, timeout.Token);
        Assert.False(held.IsCompleted);
        Assert.Equal(1, calls);

        poller.NotifyChanged();
        await held.WaitAsync(timeout.Token);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Poller_restarts_a_pass_cancelled_by_an_edit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var poller = new WorkspaceDiagnosticsPoller(TimeSpan.FromMilliseconds(10));
        var calls = 0;
        Task<WorkspaceDiagnosticReport> Compute(CancellationToken _) =>
            Interlocked.Increment(ref calls) == 1
                ? Task.FromCanceled<WorkspaceDiagnosticReport>(new CancellationToken(canceled: true))
                : Task.FromResult(new WorkspaceDiagnosticReport());

        await poller.PollAsync(new WorkspaceDiagnosticParams(), Compute, timeout.Token);

        Assert.Equal(2, calls);
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
