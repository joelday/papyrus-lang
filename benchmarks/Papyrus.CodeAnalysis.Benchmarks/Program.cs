using System;

namespace Papyrus.CodeAnalysis.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        return ParseThroughput.Run(args, Console.Out, Console.Error);
    }
}
