using System.Threading.Tasks;

namespace Papyrus.LanguageServer;

internal static class Program
{
    private static Task<int> Main()
    {
        return LanguageServerHost.RunStdioAsync();
    }
}
