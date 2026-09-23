using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class HierarchyHandlerTests
{
    // Foo is open; Other (extends Foo) and Third (extends Other) exist only on disk.
    private const string FooText =
        "ScriptName Foo\n"
        + "Function Bar()\n"
        + "  Baz()\n"
        + "EndFunction\n"
        + "Function Baz()\n"
        + "EndFunction\n"
        + "Other Property Link Auto\n";

    private static async Task<LspTestWorkspace> CreateAsync(CancellationToken ct)
    {
        var fixture = LspTestWorkspace.Create("papyrus-lsp-hierarchy-");
        fixture.WriteScript("Other", "ScriptName Other Extends Foo\nFunction Bar()\n  Baz()\n  Bar()\n  Baz()\nEndFunction\n");
        fixture.WriteScript("Third", "ScriptName Third Extends Other\n");
        await fixture.OpenAsync(FooText, ct);
        return fixture;
    }

    private static TextDocumentPositionParams At(LspTestWorkspace fixture, int line, int character) =>
        new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            Position = new Position { Line = line, Character = character },
        };

    private static DefinitionParams DefinitionAt(LspTestWorkspace fixture, int line, int character) =>
        new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = fixture.ScriptUri },
            Position = new Position { Line = line, Character = character },
        };

    [Fact]
    public async Task Type_definition_goes_to_the_property_type_script()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);

        var locations = await fixture.Scheduler.ExecuteAsync(new TypeDefinitionHandler(), DefinitionAt(fixture, 6, 16), timeout.Token);

        var location = Assert.Single(locations);
        Assert.EndsWith("/Other.psc", location.Uri);
        Assert.Equal(0, location.Range.Start!.Line);
        Assert.Equal(11, location.Range.Start!.Character);
    }

    [Fact]
    public async Task Implementation_of_a_function_finds_overrides_in_derived_scripts()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);

        var locations = await fixture.Scheduler.ExecuteAsync(new ImplementationHandler(), DefinitionAt(fixture, 1, 10), timeout.Token);

        var location = Assert.Single(locations);
        Assert.EndsWith("/Other.psc", location.Uri);
        Assert.Equal(1, location.Range.Start!.Line);
    }

    [Fact]
    public async Task Implementation_of_a_script_finds_every_derived_script()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);

        var locations = await fixture.Scheduler.ExecuteAsync(new ImplementationHandler(), DefinitionAt(fixture, 0, 12), timeout.Token);

        Assert.Equal(
            ["Other.psc", "Third.psc"],
            locations.Select(l => l.Uri[(l.Uri.LastIndexOf('/') + 1)..]).Order().ToArray());
    }

    [Fact]
    public async Task Incoming_calls_group_call_sites_by_calling_function()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);

        var item = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new PrepareCallHierarchyHandler(), At(fixture, 4, 10), timeout.Token) ?? []);
        Assert.Equal("Baz", item.Name);
        Assert.Equal("Foo", item.Detail);

        var calls = await fixture.Scheduler.ExecuteAsync(
            new IncomingCallsHandler(), new HierarchyItemParams { Item = item }, timeout.Token) ?? [];

        Assert.Equal(2, calls.Length);
        var local = Assert.Single(calls, c => c.From.Uri == fixture.ScriptUri);
        Assert.Equal("Bar", local.From.Name);
        Assert.Equal(2, Assert.Single(local.FromRanges).Start!.Line);
        var derived = Assert.Single(calls, c => c.From.Uri.EndsWith("/Other.psc", StringComparison.Ordinal));
        Assert.Equal("Bar", derived.From.Name);
        Assert.Equal([2, 4], derived.FromRanges.Select(r => r.Start!.Line).ToArray());
    }

    [Fact]
    public async Task Outgoing_calls_resolve_from_an_item_in_an_unopened_script()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);
        var baz = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new PrepareCallHierarchyHandler(), At(fixture, 4, 10), timeout.Token) ?? []);
        var caller = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new IncomingCallsHandler(), new HierarchyItemParams { Item = baz }, timeout.Token) ?? [],
            c => c.From.Uri != fixture.ScriptUri).From;

        var calls = await fixture.Scheduler.ExecuteAsync(
            new OutgoingCallsHandler(), new HierarchyItemParams { Item = caller }, timeout.Token) ?? [];

        Assert.Equal(["Baz", "Bar"], calls.Select(c => c.To.Name).ToArray());
        Assert.Equal(fixture.ScriptUri, calls[0].To.Uri);
        Assert.Equal(2, calls[0].FromRanges.Length);
        Assert.Equal(caller.Uri, calls[1].To.Uri);
    }

    [Fact]
    public async Task Type_hierarchy_walks_supertypes_and_direct_subtypes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var fixture = await CreateAsync(timeout.Token);

        var foo = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new PrepareTypeHierarchyHandler(), At(fixture, 0, 12), timeout.Token) ?? []);
        Assert.Equal("Foo", foo.Name);
        Assert.Empty(await fixture.Scheduler.ExecuteAsync(new SupertypesHandler(), new HierarchyItemParams { Item = foo }, timeout.Token) ?? []);

        var other = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new SubtypesHandler(), new HierarchyItemParams { Item = foo }, timeout.Token) ?? []);
        Assert.Equal("Other", other.Name);
        Assert.Equal("extends Foo", other.Detail);

        var third = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new SubtypesHandler(), new HierarchyItemParams { Item = other }, timeout.Token) ?? []);
        Assert.Equal("Third", third.Name);

        var super = Assert.Single(
            await fixture.Scheduler.ExecuteAsync(new SupertypesHandler(), new HierarchyItemParams { Item = third }, timeout.Token) ?? []);
        Assert.Equal("Other", super.Name);
    }
}
