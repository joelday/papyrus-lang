using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitConfig
    {
        public CreationKitPapyrusConfig Papyrus { get; set; } = new CreationKitPapyrusConfig();
    }

    public class CreationKitPapyrusConfig
    {
        public string sScriptSourceFolder { get; set; }
        public string sAdditionalImports { get; set; }
    }
}