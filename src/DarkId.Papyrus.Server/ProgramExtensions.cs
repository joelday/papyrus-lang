using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Program;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Linq;

namespace DarkId.Papyrus.Server
{
    public static class ProgramExtensions
    {
        public static void PublishDiagnostics(this ScriptFile scriptFile, ILanguageServerDocument document)
        {
            document.PublishDiagnostics(new PublishDiagnosticsParams()
            {
                Uri = PathUtilities.ToFileUri(PathUtilities.Normalize(scriptFile.FilePath)),
                Diagnostics = scriptFile.Diagnostics.Select(d => d.ToDiagnostic(scriptFile.Program.Name)).ToArray()
            });
        }
    }
}
