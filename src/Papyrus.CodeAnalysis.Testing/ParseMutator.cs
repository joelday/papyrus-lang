using System;
using System.Collections.Generic;
using System.Text;

namespace Papyrus.CodeAnalysis.Testing;

public static class ParseMutator
{
    public const int DefaultSeed = 109;

    public const int DefaultMutationCount = 24;

    private const string Alphabet =
        " \t\nABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "()[]{}.,:;\\\"'+-*/=!<>#@";

    public static IEnumerable<string> Mutations(string source, int seed, int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            yield return Mutate(source, random);
        }
    }

    public static string Mutate(string source, Random random)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(random);

        if (source.Length == 0)
        {
            return Insert(source, random);
        }

        return random.Next(4) switch
        {
            0 => Delete(source, random),
            1 => Insert(source, random),
            2 => Truncate(source, random),
            _ => Duplicate(source, random),
        };
    }

    private static string Delete(string source, Random random)
    {
        var start = random.Next(source.Length);
        var length = random.Next(1, Math.Min(8, source.Length - start) + 1);
        return source.Remove(start, length);
    }

    private static string Insert(string source, Random random)
    {
        var index = source.Length == 0 ? 0 : random.Next(source.Length + 1);
        var count = random.Next(1, 9);
        var inserted = new StringBuilder(count);
        for (var i = 0; i < count; i++)
        {
            inserted.Append(Alphabet[random.Next(Alphabet.Length)]);
        }

        return source.Insert(index, inserted.ToString());
    }

    private static string Truncate(string source, Random random)
    {
        return source.Substring(0, random.Next(source.Length));
    }

    private static string Duplicate(string source, Random random)
    {
        var start = random.Next(source.Length);
        var length = random.Next(1, Math.Min(8, source.Length - start) + 1);
        var slice = source.Substring(start, length);
        var index = random.Next(source.Length + 1);
        return source.Insert(index, slice);
    }
}
