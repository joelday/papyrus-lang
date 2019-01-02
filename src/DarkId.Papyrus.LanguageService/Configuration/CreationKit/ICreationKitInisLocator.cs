using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitIniLocations
    {
        public string CreationKitInstallPath { get; set; }
        public List<string> RelativeIniPaths { get; set; }
    }

    public interface ICreationKitInisLocator
    {
        CreationKitIniLocations GetIniLocations();
    }
}