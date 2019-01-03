using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Newtonsoft.Json.Linq;
using DarkId.Papyrus.LanguageService.Common;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.Server
{
    class CreationKitInisLocator : ICreationKitInisLocator
    {
        private readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;
        private readonly ILogger<CreationKitInisLocator> _logger;

        public CreationKitInisLocator(
            OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer,
            ILogger<CreationKitInisLocator> logger)
        {
            _languageServer = languageServer;
            _logger = logger;
        }

        public CreationKitIniLocations GetIniLocations()
        {
            _logger.LogInformation("Resolving CreationKit ini files...");

            var configuration = _languageServer.Workspace.GetConfiguration();

            return new CreationKitIniLocations()
            {
                CreationKitInstallPath =
                    configuration?.InstallPath ??
                    "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Fallout 4",
                RelativeIniPaths =
                    configuration?.CreationKitIniFiles ??
                    new List<string>() { "CreationKit.ini", "CreationKitCustom.ini" }
            };
        }
    }
}
