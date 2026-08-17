using System;

namespace Papyrus.Compiler;

internal static class Program
{
    private static int Main(string[] args)
    {
        return Cli.Run(args, Console.Out, Console.Error);
    }
}
