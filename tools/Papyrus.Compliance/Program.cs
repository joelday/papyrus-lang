using System;

namespace Papyrus.Compliance;

internal static class Program
{
    private static int Main(string[] args)
    {
        return Cli.Run(args, Console.Out, Console.Error);
    }
}
