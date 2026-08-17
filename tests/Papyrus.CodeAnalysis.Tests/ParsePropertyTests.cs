using System;
using System.Collections.Generic;
using System.Threading;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Testing;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ParsePropertyTests
{
    // Add a seed here when the fuzzer finds a failure.
    private static readonly int[] CommittedSeeds = { ParseMutator.DefaultSeed };

    [Fact(Timeout = 10_000)]
    public void Round_trip_ToFullString_matches_source()
    {
        foreach (var fixture in LoadFixtures())
        {
            var tree = SyntaxTree.Parse(fixture.Source, fixture.Dialect);
            var actual = tree.GetRoot().ToFullString();
            if (actual != fixture.Source)
            {
                Assert.Fail(
                    fixture.Name + " did not round-trip. Expected length " +
                    fixture.Source.Length + ", actual " + actual.Length + ".");
            }
        }
    }

    [Fact(Timeout = 30_000)]
    public void Prefix_parses_every_character_without_throw_or_hang()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        foreach (var fixture in LoadFixtures())
        {
            for (var length = 0; length <= fixture.Source.Length; length++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prefix = fixture.Source.Substring(0, length);
                AssertParseDoesNotThrow(prefix, fixture.Dialect, fixture.Name + " prefix length " + length, cancellationToken);
            }
        }
    }

    [Fact(Timeout = 30_000)]
    public void Mutation_fuzzer_does_not_throw()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        foreach (var seed in CommittedSeeds)
        {
            foreach (var fixture in LoadFixtures())
            {
                var index = 0;
                foreach (var mutated in ParseMutator.Mutations(fixture.Source, seed, ParseMutator.DefaultMutationCount))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AssertParseDoesNotThrow(
                        mutated,
                        fixture.Dialect,
                        fixture.Name + " seed " + seed + " mutation " + index,
                        cancellationToken);
                    index++;
                }
            }
        }
    }

    private static IReadOnlyList<ParseFixture> LoadFixtures()
    {
        return ParseFixtureCatalog.Load(ParseFixtureCatalog.FindDirectory());
    }

    private static void AssertParseDoesNotThrow(
        string source,
        PapyrusDialect dialect,
        string label,
        CancellationToken cancellationToken)
    {
        SyntaxTree tree;
        try
        {
            tree = SyntaxTree.Parse(SourceText.From(source), dialect, options: null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Assert.Fail(label + " threw " + ex.GetType().Name + ": " + ex.Message);
            return;
        }

        var actual = tree.GetRoot().ToFullString();
        if (actual != source)
        {
            Assert.Fail(
                label + " did not round-trip. Expected length " +
                source.Length + ", actual " + actual.Length + ".");
        }
    }
}
