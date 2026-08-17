using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Papyrus.CodeAnalysis;

[EventSource(Name = EventSourceName)]
public sealed class PapyrusEventSource : EventSource
{
    public const string EventSourceName = "Papyrus-CodeAnalysis";

    public static PapyrusEventSource Log { get; } = new();

    private readonly ConcurrentDictionary<string, long> _diagnosticCounts = new(StringComparer.Ordinal);

    private long _parseCount;
    private long _parseCacheHits;
    private long _bindCount;
    private long _applyChangeCount;
    private long _diagnosticCount;

    private PollingCounter? _parseCountCounter;
    private IncrementingPollingCounter? _parseCacheHitsCounter;
    private IncrementingPollingCounter? _bindCountCounter;
    private IncrementingPollingCounter? _applyChangeCountCounter;
    private IncrementingPollingCounter? _diagnosticCountCounter;

    private PapyrusEventSource()
    {
    }

    public long ParseCount => Volatile.Read(ref _parseCount);

    public long ParseCacheHits => Volatile.Read(ref _parseCacheHits);

    public long BindCount => Volatile.Read(ref _bindCount);

    public long ApplyChangeCount => Volatile.Read(ref _applyChangeCount);

    public long DiagnosticCount => Volatile.Read(ref _diagnosticCount);

    public long GetDiagnosticCount(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _diagnosticCounts.TryGetValue(id, out var count) ? count : 0;
    }

    public IReadOnlyDictionary<string, long> GetDiagnosticCounts()
    {
        return new Dictionary<string, long>(_diagnosticCounts, StringComparer.Ordinal);
    }

    [NonEvent]
    public void Parse()
    {
        Interlocked.Increment(ref _parseCount);
        if (IsEnabled())
        {
            WriteParse();
        }
    }

    [NonEvent]
    public void ParseCacheHit()
    {
        Interlocked.Increment(ref _parseCacheHits);
        if (IsEnabled())
        {
            WriteParseCacheHit();
        }
    }

    [NonEvent]
    public void Bind()
    {
        Interlocked.Increment(ref _bindCount);
        if (IsEnabled())
        {
            WriteBind();
        }
    }

    [NonEvent]
    public void ApplyChange()
    {
        Interlocked.Increment(ref _applyChangeCount);
        if (IsEnabled())
        {
            WriteApplyChange();
        }
    }

    [NonEvent]
    public void Diagnostic(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Interlocked.Increment(ref _diagnosticCount);
        _diagnosticCounts.AddOrUpdate(id, 1, static (_, count) => count + 1);
        if (IsEnabled())
        {
            WriteDiagnostic(id);
        }
    }

    [NonEvent]
    internal void AddDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsDefaultOrEmpty)
        {
            return;
        }

        for (var i = 0; i < diagnostics.Length; i++)
        {
            Diagnostic(diagnostics[i].Id);
        }
    }

    [Event(1)]
    private void WriteParse() => WriteEvent(1);

    [Event(2)]
    private void WriteParseCacheHit() => WriteEvent(2);

    [Event(3)]
    private void WriteBind() => WriteEvent(3);

    [Event(4)]
    private void WriteApplyChange() => WriteEvent(4);

    [Event(5)]
    private void WriteDiagnostic(string id) => WriteEvent(5, id);

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command != EventCommand.Enable)
        {
            return;
        }

        _parseCountCounter ??= new PollingCounter("parse-count", this, () => Volatile.Read(ref _parseCount))
        {
            DisplayName = "Parse Count",
        };
        _parseCacheHitsCounter ??= new IncrementingPollingCounter(
            "parse-cache-hits",
            this,
            () => Volatile.Read(ref _parseCacheHits))
        {
            DisplayName = "Parse Cache Hits",
        };
        _bindCountCounter ??= new IncrementingPollingCounter("bind-count", this, () => Volatile.Read(ref _bindCount))
        {
            DisplayName = "Bind Count",
        };
        _applyChangeCountCounter ??= new IncrementingPollingCounter(
            "workspace-apply-change",
            this,
            () => Volatile.Read(ref _applyChangeCount))
        {
            DisplayName = "Workspace ApplyChange",
        };
        _diagnosticCountCounter ??= new IncrementingPollingCounter(
            "diagnostic-count",
            this,
            () => Volatile.Read(ref _diagnosticCount))
        {
            DisplayName = "Diagnostic Count",
        };
    }
}
