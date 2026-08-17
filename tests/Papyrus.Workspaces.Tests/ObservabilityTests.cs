using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class ObservabilityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "papyrus-obs-" + Guid.NewGuid().ToString("N"));

    public ObservabilityTests()
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
    public void ApplyChange_starts_activity_and_increments_counter()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var started = new ConcurrentBag<(string Name, string? ChangeKind)>();
        using var listener = Listen(activity =>
            started.Add((activity.OperationName, activity.GetTagItem("change.kind") as string)));
        var before = PapyrusEventSource.Log.ApplyChangeCount;

        workspace.ApplyChange(
            new WorkspaceChange.OpenDocument(
                DocumentId.Create(project.Id, diskPath),
                SourceText.From("ScriptName Dirty\n")));

        Assert.True(PapyrusEventSource.Log.ApplyChangeCount > before);
        Assert.Contains(
            started,
            static activity => activity.Name == "workspace.applyChange" && activity.ChangeKind == "OpenDocument");
    }

    [Fact]
    public void Describe_reports_workspace_shape_without_source()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var empty = workspace.Describe();
        Assert.Equal(0, empty.ProjectCount);
        Assert.Equal(0, empty.OpenDocumentCount);
        Assert.Null(empty.LastChangeKind);

        var project = workspace.LoadProject(ppj);
        var documentId = DocumentId.Create(project.Id, diskPath);
        workspace.ApplyChange(new WorkspaceChange.OpenDocument(documentId, SourceText.From("ScriptName Dirty\n")));

        var snapshot = workspace.Describe();
        var solutionSnapshot = workspace.CurrentSolution.Describe();

        Assert.Equal(1, snapshot.ProjectCount);
        Assert.Equal(1, snapshot.OpenDocumentCount);
        Assert.Equal("OpenDocument", snapshot.LastChangeKind);
        Assert.Contains("SkyrimSpecialEdition", snapshot.Dialects);
        Assert.Contains(Path.GetFullPath(ppj), snapshot.ProjectPaths);
        Assert.Contains(documentId.Path, snapshot.OpenDocumentPaths);
        Assert.Equal(1, solutionSnapshot.ProjectCount);
        Assert.Null(solutionSnapshot.LastChangeKind);
        Assert.DoesNotContain("ScriptName Dirty", snapshot.ProjectPaths);
        Assert.DoesNotContain("ScriptName Dirty", snapshot.OpenDocumentPaths);
    }

    private PapyrusWorkspace CreateWorkspace()
    {
        return new PapyrusWorkspace(
            TimeProvider.System,
            watchDebounce: TimeSpan.FromMilliseconds(100),
            enableFileWatching: false);
    }

    private string CreateScriptsDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string WriteProject(string fileName, string import)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(
            path,
            $"""
            <PapyrusProject Game="sse">
              <Imports>
                <Import>{import}</Import>
              </Imports>
            </PapyrusProject>
            """);
        return path;
    }

    private static ActivityListener Listen(Action<Activity> onStarted)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == PapyrusActivitySource.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = onStarted,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
