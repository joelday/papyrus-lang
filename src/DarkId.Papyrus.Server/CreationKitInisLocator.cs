using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    class CreationKitInisLocator : ICreationKitInisLocator
    {
        private readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;

        public CreationKitInisLocator(OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer)
        {
            _languageServer = languageServer;
        }

        public CreationKitIniLocations GetIniLocations()
        {
            return new CreationKitIniLocations()
            {
                CreationKitInstallPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Fallout 4",
                RelativeIniPaths = new List<string>() { "CreationKit.ini", "CreationKitCustom.ini" }
            };
        }
    }
}
