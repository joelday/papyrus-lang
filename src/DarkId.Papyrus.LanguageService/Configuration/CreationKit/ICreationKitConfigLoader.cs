using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public interface ICreationKitConfigLoader
    {
        CreationKitConfigInfo LoadConfig(CreationKitIniLocations locations);
    }
}