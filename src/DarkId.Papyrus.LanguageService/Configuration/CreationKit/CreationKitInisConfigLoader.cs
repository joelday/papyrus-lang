using System;
using System.IO;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitInisConfigLoader : ICreationKitConfigLoader
    {
        public CreationKitConfigInfo LoadConfig(CreationKitIniLocations inis)
        {
            var configBuilder = new ConfigurationBuilder();

            foreach (var iniPath in inis.RelativeIniPaths)
            {
                configBuilder.AddIniFile(
                    Path.GetFullPath(Path.Combine(
                        PathUtilities.Normalize(inis.CreationKitInstallPath),
                        PathUtilities.Normalize(iniPath))),
                        true);
            }

            var configuration = configBuilder.Build();
            var creationKitConfig = new CreationKitConfig();
            configuration.Bind(creationKitConfig);

            return new CreationKitConfigInfo()
            {
                CreationKitInstallPath = inis.CreationKitInstallPath,
                Config = creationKitConfig
            };
        }
    }
}