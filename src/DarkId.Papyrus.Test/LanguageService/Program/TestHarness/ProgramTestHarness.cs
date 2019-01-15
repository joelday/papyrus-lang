using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Compiler;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using DarkId.Papyrus.LanguageService.Program;
using Microsoft.Extensions.DependencyInjection;

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
#if FALLOUT4
                        "Fallout4.ini"
#elif SKYRIM
                        "Skyrim.ini"
#endif
                    }
                };
            }
        }

        private readonly static IServiceProvider _serviceProvider;

        static ProgramTestHarness()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IFileSystem, LocalFileSystem>()
                .AddSingleton<IScriptTextProvider, FileSystemScriptTextProvider>((provider) =>
                {
                    var textProvider = new FileSystemScriptTextProvider(provider.GetService<IFileSystem>());
                    AntlrPatch.SetTextProvider(textProvider);
                    return textProvider;
                })
                .AddSingleton<ICreationKitInisLocator, CreationKitInisLocator>()
                .AddSingleton<ICreationKitConfigLoader, CreationKitInisConfigLoader>()
                .AddSingleton<CreationKitProgramOptionsProvider>();

            _serviceProvider = serviceCollection.BuildServiceProvider();

            HarmonyPatches.Apply();
        }

        public static PapyrusProgram CreateProgram()
        {
            var programOptionsProvider = _serviceProvider.GetService<CreationKitProgramOptionsProvider>();
            var options = programOptionsProvider.GetAmbientProgramOptions();

            return _serviceProvider.CreateInstance<PapyrusProgram>(options);
        }
    }
}
