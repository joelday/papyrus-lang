using System;
using System.IO;
using System.Threading.Tasks;

namespace Papyrus.LanguageServer;

internal static class Program
{
    // GetName().Version is AOT-safe; GetCustomAttribute is annotated RequiresUnreferencedCode.
    internal static string Version =>
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private static Task<int> Main(string[] args)
    {
        if (TryHandleCli(args, Console.Out))
        {
            return Task.FromResult(0);
        }

        return LanguageServerHost.RunStdioAsync();
    }

    internal static bool TryHandleCli(string[] args, TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);

        foreach (var arg in args)
        {
            if (arg is "--version" or "-V")
            {
                stdout.WriteLine(Version);
                return true;
            }
        }

        return false;
    }
}
