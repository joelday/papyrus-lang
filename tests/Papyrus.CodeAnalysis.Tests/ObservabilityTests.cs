using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public void Parse_starts_parse_activity_and_increments_counter()
    {
        var started = new ConcurrentBag<string>();
        using var listener = Listen(activity => started.Add(activity.OperationName));
        var before = PapyrusEventSource.Log.ParseCount;

        _ = SyntaxTree.Parse("ScriptName Foo\n", PapyrusDialect.SkyrimSe);

        Assert.True(PapyrusEventSource.Log.ParseCount > before);
        Assert.Contains("parse", started);
    }

    [Fact]
    public void GetScript_starts_bind_activity_and_increments_counter()
    {
        var started = new ConcurrentBag<(string Name, string? Script)>();
        using var listener = Listen(activity => started.Add((activity.OperationName, activity.GetTagItem("script") as string)));
        var name = QualifiedName.Parse("ObsBind");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName ObsBind\nFunction Bar()\nEndFunction\n"),
            }));
        var before = PapyrusEventSource.Log.BindCount;

        _ = compilation.GetScript(name);

        Assert.True(PapyrusEventSource.Log.BindCount > before);
        Assert.Contains(started, static activity => activity.Name == "bind" && activity.Script == "ObsBind");
        Assert.Contains(started, static activity => activity.Name == "parse");
    }

    [Fact]
    public void GetSyntaxTree_cache_hit_increments_parse_cache_hits()
    {
        var name = QualifiedName.Parse("ObsCache");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName ObsCache\n"),
            }));

        _ = compilation.GetSyntaxTree(name);
        var hitsBefore = PapyrusEventSource.Log.ParseCacheHits;

        _ = compilation.GetSyntaxTree(name);

        // ParseCount is process-wide; other tests may parse concurrently.
        Assert.True(PapyrusEventSource.Log.ParseCacheHits > hitsBefore);
    }

    [Fact]
    public void GetDiagnostics_increments_diagnostic_count_by_id()
    {
        var compilation = PapyrusCompilation.Create(PapyrusDialect.SkyrimSe, new TestScriptResolver());
        _ = compilation.GetScript(QualifiedName.Parse("MissingObs"));
        var before = PapyrusEventSource.Log.GetDiagnosticCount(PapyrusCompilation.UnresolvedScriptDiagnosticId);
        var totalBefore = PapyrusEventSource.Log.DiagnosticCount;

        var diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken);

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == PapyrusCompilation.UnresolvedScriptDiagnosticId);
        Assert.True(PapyrusEventSource.Log.GetDiagnosticCount(PapyrusCompilation.UnresolvedScriptDiagnosticId) > before);
        Assert.True(PapyrusEventSource.Log.DiagnosticCount > totalBefore);
        Assert.True(
            PapyrusEventSource.Log.GetDiagnosticCounts().ContainsKey(PapyrusCompilation.UnresolvedScriptDiagnosticId));
    }

    [Fact]
    public void EventSource_listener_sees_parse_and_bind_events()
    {
        using var listener = new RecordingEventListener();
        var name = QualifiedName.Parse("ObsEvents");
        var compilation = PapyrusCompilation.Create(
            PapyrusDialect.SkyrimSe,
            new TestScriptResolver(new Dictionary<QualifiedName, SourceText>
            {
                [name] = SourceText.From("ScriptName ObsEvents\n"),
            }));

        _ = SyntaxTree.Parse("ScriptName ObsEventsParse\n", PapyrusDialect.SkyrimSe);
        _ = compilation.GetScript(name);

        Assert.True(listener.ParseEvents > 0);
        Assert.True(listener.BindEvents > 0);
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

    private sealed class RecordingEventListener : EventListener
    {
        public int ParseEvents;
        public int BindEvents;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == PapyrusEventSource.EventSourceName)
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == "WriteParse")
            {
                ParseEvents++;
            }
            else if (eventData.EventName == "WriteBind")
            {
                BindEvents++;
            }
        }
    }
}
