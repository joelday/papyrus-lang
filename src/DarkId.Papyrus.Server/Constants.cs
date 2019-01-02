using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DarkId.Papyrus.Server
{
    public static class Constants
    {
        public static readonly DocumentFilter PapyrusScriptDocumentFilter = new DocumentFilter()
        {
            Language = "papyrus",
            Scheme = "file"
        };

        public static readonly DocumentFilter PapyrusProjectDocumentFilter = new DocumentFilter()
        {
            Language = "papyrus-project",
            Scheme = "file"
        };

        public static readonly DocumentSelector PapyrusScriptSelector = new DocumentSelector(PapyrusScriptDocumentFilter);
        public static readonly DocumentSelector PapyrusProjectSelector = new DocumentSelector(PapyrusProjectDocumentFilter);
        public static readonly DocumentSelector PapyrusProjectsAndScriptsSelector =
            new DocumentSelector(PapyrusScriptDocumentFilter, PapyrusProjectDocumentFilter);
    }
}