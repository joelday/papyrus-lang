using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using DarkId.Papyrus.LanguageService.Program;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService
{
    public class TestServiceInstance
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class CreationKitInisLocator : ICreationKitInisLocator
        {
            private readonly LanguageVersion _languageVersion;

            public CreationKitInisLocator(LanguageVersion languageVersion)
            {
                _languageVersion = languageVersion;
            }

            public CreationKitIniLocations GetIniLocations()
            {
                var scriptsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../scripts");

                return new CreationKitIniLocations()
                {
                    CreationKitInstallPath = scriptsPath,
                    RelativeIniPaths = new List<string>()
                    {
                        _languageVersion == LanguageVersion.Fallout4 ? "Fallout4.ini" :  "Skyrim.ini"
                    }
                };
            }
        }

        private readonly IServiceProvider _serviceProvider;

        public TestServiceInstance(LanguageVersion languageVersion)
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IFileSystem, LocalFileSystem>()
                .AddSingleton<IScriptTextProvider, FileSystemScriptTextProvider>()
                .AddSingleton<ICreationKitInisLocator, CreationKitInisLocator>((provider) =>
                    provider.CreateInstance<CreationKitInisLocator>(languageVersion)
                )
                .AddSingleton<ICreationKitConfigLoader, CreationKitInisConfigLoader>()
                .AddSingleton((provider) =>
                    provider.CreateInstance<CreationKitProgramOptionsProvider>(
                        languageVersion,
                        "Creation Kit",
                        languageVersion == LanguageVersion.Fallout4 ? "Institute_Papyrus_Flags.flg" : "TESV_Papyrus_Flags.flg",
                        new CreationKitConfig()));

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        public PapyrusProgram CreateProgram()
        {
            var programOptionsProvider = _serviceProvider.GetService<CreationKitProgramOptionsProvider>();
            var options = programOptionsProvider.GetAmbientProgramOptions();

            options.Sources.Includes.Insert(0, new SourceInclude()
            {
                Path = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../FO4Scripts/Base"),
                IsImport = true,
                Recursive = true
            });

            return _serviceProvider.CreateInstance<PapyrusProgram>(options);
        }
    }
}
