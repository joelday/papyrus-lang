using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    //[Papyrus]
    //sScriptSourceFolder="../Source/Scripts/" <-- wat?
    //sAdditionalImports=""
    //sScriptTempFolder=""
    //sScriptCompiledFolder="Data/Scripts/"
    //sCompilerFolder="Papyrus Compiler/"

    public class CreationKitConfig
    {
        public CreationKitPapyrusConfig Papyrus { get; set; }
    }

    public class CreationKitPapyrusConfig
    {
        public string sScriptSourceFolder { get; set; }
        public string sAdditionalImports { get; set; }
        public string sCompilerFolder { get; set; } = "Papyrus Compiler/";
    }
}