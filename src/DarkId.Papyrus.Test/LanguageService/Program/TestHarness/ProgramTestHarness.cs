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

namespace DarkId.Papyrus.Test.LanguageService.Program.TestHarness
{
    class ProgramTestHarness
    {
        private class CreationKitInisLocator : ICreationKitInisLocator
        {
            public CreationKitIniLocations GetIniLocations()
            {
                return new CreationKitIniLocations()
                {
                    CreationKitInstallPath = "../../../../scripts",
                    RelativeIniPaths = new List<string>() {
//#if FALLOUT4
                        "Fallout4.ini"
//#elif SKYRIM
//                        "Skyrim.ini"
//#endif
                    }
                };
            }
        }

        private static readonly IServiceProvider _serviceProvider;

        static ProgramTestHarness()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IFileSystem, LocalFileSystem>()
                .AddSingleton<IScriptTextProvider, FileSystemScriptTextProvider>()
                .AddSingleton<ICreationKitInisLocator, CreationKitInisLocator>()
                .AddSingleton<ICreationKitConfigLoader, CreationKitInisConfigLoader>()
                .AddSingleton((provider) =>
                    provider.CreateInstance<CreationKitProgramOptionsProvider>(
                        "Creation Kit",
//// TODO: Configured
//#if FALLOUT4
                        "Institute_Papyrus_Flags.flg",
//#elif SKYRIM
//                        "TESV_Papyrus_Flags.flg",
//#endif
                        new CreationKitConfig()));

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        public static PapyrusProgram CreateProgram()
        {
            var programOptionsProvider = _serviceProvider.GetService<CreationKitProgramOptionsProvider>();
            var options = programOptionsProvider.GetAmbientProgramOptions();

            return _serviceProvider.CreateInstance<PapyrusProgram>(options);
        }
    }
}
