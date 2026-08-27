using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Workspaces;
using Xunit;

namespace Papyrus.Workspaces.Tests;

public sealed class PapyrusWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "papyrus-workspace-" + Guid.NewGuid().ToString("N"));

    public PapyrusWorkspaceTests()
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
    public void LoadProject_sets_path_and_creates_compilation()
    {
        var scripts = CreateScriptsDirectory("scripts");
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);

        Assert.Equal(Path.GetFullPath(ppj), project.Path);
        Assert.Equal(ProjectId.Create(ppj), project.Id);
        var compilation = workspace.CurrentSolution.GetCompilation(project.Id);
        Assert.Equal("Foo", compilation.GetScript(QualifiedName.Parse("Foo")).Name.Text);
        Assert.Equal(
            "ScriptName Foo\n",
            compilation.GetSyntaxTree(QualifiedName.Parse("Foo")).GetText().ToString());
    }

    [Fact]
    public void Dirty_buffer_shadows_disk()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Disk\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var documentId = DocumentId.Create(project.Id, diskPath);
        workspace.ApplyChange(
            new WorkspaceChange.OpenDocument(documentId, SourceText.From("ScriptName Dirty\n")));

        Assert.Equal("ScriptName Dirty\n", ReadScript(workspace, project.Id, "Foo"));
        File.WriteAllText(diskPath, "ScriptName Disk2\n");
        workspace.ApplyChange(new WorkspaceChange.FileCreated(diskPath));
        Assert.Equal("ScriptName Dirty\n", ReadScript(workspace, project.Id, "Foo"));
    }

    [Fact]
    public void Close_reverts_to_disk()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Disk\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var documentId = DocumentId.Create(project.Id, diskPath);
        workspace.ApplyChange(
            new WorkspaceChange.OpenDocument(documentId, SourceText.From("ScriptName Dirty\n")));
        workspace.ApplyChange(
            new WorkspaceChange.ChangeDocument(documentId, SourceText.From("ScriptName Edited\n")));

        Assert.Equal("ScriptName Edited\n", ReadScript(workspace, project.Id, "Foo"));

        File.WriteAllText(diskPath, "ScriptName Disk2\n");
        workspace.ApplyChange(new WorkspaceChange.CloseDocument(documentId));

        Assert.False(workspace.CurrentSolution.IsDocumentOpen(documentId));
        Assert.Equal("ScriptName Disk2\n", ReadScript(workspace, project.Id, "Foo"));
    }

    [Fact]
    public void Reload_project_rebuilds_includes()
    {
        var first = CreateScriptsDirectory("first");
        var second = CreateScriptsDirectory("second");
        File.WriteAllText(Path.Combine(first, "Old.psc"), "ScriptName Old\n");
        File.WriteAllText(Path.Combine(second, "New.psc"), "ScriptName New\n");
        var ppj = WriteProject("game.ppj", "first");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var compilation = workspace.CurrentSolution.GetCompilation(project.Id);
        Assert.True(compilation.Resolver.TryResolve(QualifiedName.Parse("Old"), out _, out _));
        Assert.False(compilation.Resolver.TryResolve(QualifiedName.Parse("New"), out _, out _));

        WriteProject("game.ppj", "second");
        workspace.ApplyChange(new WorkspaceChange.ReloadProject(project.Id));

        var reloaded = workspace.CurrentSolution.GetCompilation(project.Id);
        Assert.NotSame(compilation, reloaded);
        Assert.False(reloaded.Resolver.TryResolve(QualifiedName.Parse("Old"), out _, out _));
        Assert.True(reloaded.Resolver.TryResolve(QualifiedName.Parse("New"), out var text, out _));
        Assert.Equal("ScriptName New\n", text.ToString());
        Assert.Equal(["second"], workspace.CurrentSolution.GetProject(project.Id).Imports);
    }

    [Fact]
    public void ApplyChange_fires_one_event_per_call()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var events = Subscribe(workspace);

        var documentId = DocumentId.Create(project.Id, diskPath);
        workspace.ApplyChange(
            new WorkspaceChange.OpenDocument(documentId, SourceText.From("ScriptName One\n")));
        workspace.ApplyChange(
            new WorkspaceChange.ChangeDocument(documentId, SourceText.From("ScriptName Two\n")));
        workspace.ApplyChange(new WorkspaceChange.CloseDocument(documentId));

        Assert.Equal(3, events.Count);
        Assert.IsType<WorkspaceChange.OpenDocument>(Assert.Single(events[0].Changes));
        Assert.IsType<WorkspaceChange.ChangeDocument>(Assert.Single(events[1].Changes));
        Assert.IsType<WorkspaceChange.CloseDocument>(Assert.Single(events[2].Changes));
    }

    [Fact]
    public void Watch_events_coalesce_into_one_workspace_event()
    {
        var scripts = CreateScriptsDirectory("scripts");
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");
        var clock = new ManualTimeProvider();

        using var workspace = CreateWorkspace(clock);
        var project = workspace.LoadProject(ppj);
        var compilation = workspace.CurrentSolution.GetCompilation(project.Id);
        _ = compilation.GetSyntaxTree(QualifiedName.Parse("A"));
        _ = compilation.GetSyntaxTree(QualifiedName.Parse("B"));
        var events = Subscribe(workspace);

        var createdA = Path.Combine(scripts, "A.psc");
        var createdB = Path.Combine(scripts, "B.psc");
        File.WriteAllText(createdA, "ScriptName A\n");
        File.WriteAllText(createdB, "ScriptName B\n");

        workspace.NotifyWatchEvent(new WorkspaceChange.FileCreated(createdA));
        workspace.NotifyWatchEvent(new WorkspaceChange.FileCreated(createdB));

        Assert.Empty(events);
        Assert.Same(compilation, workspace.CurrentSolution.GetCompilation(project.Id));
        Assert.Empty(compilation.GetScript(QualifiedName.Parse("A")).DeclaringSyntaxReferences);

        clock.Advance(TimeSpan.FromMilliseconds(50));
        workspace.FlushWatchEventsIfDebounceElapsed();
        Assert.Empty(events);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        workspace.FlushWatchEventsIfDebounceElapsed();

        var args = Assert.Single(events);
        Assert.Equal(2, args.Changes.Length);
        Assert.IsType<WorkspaceChange.FileCreated>(args.Changes[0]);
        Assert.IsType<WorkspaceChange.FileCreated>(args.Changes[1]);
        var flushed = workspace.CurrentSolution.GetCompilation(project.Id);
        Assert.NotSame(compilation, flushed);
        Assert.Equal("ScriptName A\n", flushed.GetSyntaxTree(QualifiedName.Parse("A")).GetText().ToString());
        Assert.Equal("ScriptName B\n", flushed.GetSyntaxTree(QualifiedName.Parse("B")).GetText().ToString());
    }

    [Fact]
    public void Coalesced_watch_events_for_same_path_keep_last()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        workspace.LoadProject(ppj);
        var events = Subscribe(workspace);

        workspace.NotifyWatchEvent(new WorkspaceChange.FileCreated(diskPath));
        workspace.NotifyWatchEvent(new WorkspaceChange.FileDeleted(diskPath));
        workspace.FlushPendingWatchEvents();

        var args = Assert.Single(events);
        var deleted = Assert.IsType<WorkspaceChange.FileDeleted>(Assert.Single(args.Changes));
        Assert.Equal(WorkspacePaths.NormalizeFull(diskPath), WorkspacePaths.NormalizeFull(deleted.Path));
    }

    [Fact]
    public void FileCreated_makes_new_script_resolvable()
    {
        var scripts = CreateScriptsDirectory("scripts");
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var compilation = workspace.CurrentSolution.GetCompilation(project.Id);
        _ = compilation.GetSyntaxTree(QualifiedName.Parse("Bar"));

        var barPath = Path.Combine(scripts, "Bar.psc");
        File.WriteAllText(barPath, "ScriptName Bar\n");
        Assert.Empty(compilation.GetScript(QualifiedName.Parse("Bar")).DeclaringSyntaxReferences);

        workspace.ApplyChange(new WorkspaceChange.FileCreated(barPath));

        Assert.NotSame(compilation, workspace.CurrentSolution.GetCompilation(project.Id));
        Assert.Equal("ScriptName Bar\n", ReadScript(workspace, project.Id, "Bar"));
    }

    [Fact]
    public void FileDeleted_of_ppj_removes_project()
    {
        var scripts = CreateScriptsDirectory("scripts");
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        workspace.ApplyChange(new WorkspaceChange.FileDeleted(ppj));

        Assert.False(workspace.CurrentSolution.ContainsProject(project.Id));
        Assert.Null(workspace.CurrentSolution.Compilation);
    }

    [Fact]
    public void Close_of_unknown_document_is_noop()
    {
        var scripts = CreateScriptsDirectory("scripts");
        File.WriteAllText(Path.Combine(scripts, "Foo.psc"), "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var solution = workspace.CurrentSolution;
        var events = Subscribe(workspace);

        workspace.ApplyChange(
            new WorkspaceChange.CloseDocument(DocumentId.Create(project.Id, Path.Combine(scripts, "Missing.psc"))));

        Assert.Same(solution, workspace.CurrentSolution);
        Assert.Empty(events);
    }

    [Fact]
    public void OpenDocument_unknown_project_throws()
    {
        using var workspace = CreateWorkspace();
        var id = DocumentId.Create(ProjectId.Create(Path.Combine(_root, "missing.ppj")), Path.Combine(_root, "Foo.psc"));
        Assert.Throws<ArgumentException>(
            () => workspace.ApplyChange(new WorkspaceChange.OpenDocument(id, SourceText.From("ScriptName Foo\n"))));
    }

    [Fact]
    public void ApplyChange_rebinds_dependents_and_keeps_unrelated_script()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var actorPath = Path.Combine(scripts, "Actor.psc");
        File.WriteAllText(actorPath, "ScriptName Actor\nFunction Old()\nEndFunction\n");
        File.WriteAllText(Path.Combine(scripts, "Quest.psc"), "ScriptName Quest Extends Actor\n");
        File.WriteAllText(Path.Combine(scripts, "Widget.psc"), "ScriptName Widget\nFunction Idle()\nEndFunction\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var compilation = workspace.CurrentSolution.GetCompilation(project.Id);
        var actor = compilation.GetScript(QualifiedName.Parse("Actor"));
        var quest = compilation.GetScript(QualifiedName.Parse("Quest"));
        var widget = compilation.GetScript(QualifiedName.Parse("Widget"));
        Assert.Same(actor, quest.BaseType);

        workspace.ApplyChange(
            new WorkspaceChange.OpenDocument(
                DocumentId.Create(project.Id, actorPath),
                SourceText.From("ScriptName Actor\nFunction Old()\nEndFunction\nFunction NewMember()\nEndFunction\n")));

        var updated = workspace.CurrentSolution.GetCompilation(project.Id);
        var newActor = updated.GetScript(QualifiedName.Parse("Actor"));
        var newQuest = updated.GetScript(QualifiedName.Parse("Quest"));
        var newWidget = updated.GetScript(QualifiedName.Parse("Widget"));

        Assert.Same(widget, newWidget);
        Assert.NotSame(quest, newQuest);
        Assert.NotSame(actor, newActor);
        Assert.Same(newActor, newQuest.BaseType);
        Assert.Contains(
            newQuest.BaseType!.GetMembers(),
            static member => member.Name.Text.Equals("NewMember", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClassifyWatchEvent_maps_ppj_change_to_reload()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var ppj = WriteProject("game.ppj", "scripts");
        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);

        var reload = PapyrusWorkspace.ClassifyWatchEvent(
            WatcherChangeTypes.Changed,
            ppj,
            workspace.CurrentSolution);
        var deleted = PapyrusWorkspace.ClassifyWatchEvent(
            WatcherChangeTypes.Deleted,
            ppj,
            workspace.CurrentSolution);
        var created = PapyrusWorkspace.ClassifyWatchEvent(
            WatcherChangeTypes.Created,
            Path.Combine(scripts, "Bar.psc"),
            workspace.CurrentSolution);

        var reloadChange = Assert.IsType<WorkspaceChange.ReloadProject>(reload);
        Assert.Equal(project.Id, reloadChange.Id);
        Assert.IsType<WorkspaceChange.FileDeleted>(deleted);
        Assert.IsType<WorkspaceChange.FileCreated>(created);
    }

    [Fact]
    public void TryGetSyntaxTree_maps_document_ids_to_trees()
    {
        var scripts = CreateScriptsDirectory("scripts");
        var diskPath = Path.Combine(scripts, "Foo.psc");
        File.WriteAllText(diskPath, "ScriptName Foo\n");
        var ppj = WriteProject("game.ppj", "scripts");

        using var workspace = CreateWorkspace();
        var project = workspace.LoadProject(ppj);
        var solution = workspace.CurrentSolution;

        Assert.True(solution.TryGetSyntaxTree(DocumentId.Create(project.Id, diskPath), out var tree));
        Assert.Equal("ScriptName Foo\n", tree.GetText().ToString());

        Assert.False(solution.TryGetSyntaxTree(
            DocumentId.Create(project.Id, Path.Combine(_root, "Elsewhere.psc")),
            out _));
    }

    [Fact]
    public void LoadProject_null_path_throws()
    {
        using var workspace = CreateWorkspace();
        Assert.Throws<ArgumentNullException>(() => workspace.LoadProject(null!));
        Assert.Throws<ArgumentException>(() => workspace.LoadProject("   "));
        Assert.Throws<ArgumentNullException>(() => workspace.ApplyChange(null!));
    }

    private PapyrusWorkspace CreateWorkspace(TimeProvider? timeProvider = null)
    {
        return new PapyrusWorkspace(
            timeProvider ?? TimeProvider.System,
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

    private static string ReadScript(PapyrusWorkspace workspace, ProjectId projectId, string name)
    {
        return workspace.CurrentSolution
            .GetCompilation(projectId)
            .GetSyntaxTree(QualifiedName.Parse(name))
            .GetText()
            .ToString();
    }

    private static List<WorkspaceChangeEventArgs> Subscribe(PapyrusWorkspace workspace)
    {
        var events = new List<WorkspaceChangeEventArgs>();
        workspace.WorkspaceChanged += (_, args) => events.Add(args);
        return events;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
