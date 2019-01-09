using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Compiler;
using DarkId.Papyrus.LanguageService.Program;
using Microsoft.Extensions.DependencyInjection;

namespace DarkId.Papyrus.Test.LanguageService.Program.TestHarness
{
    class ProgramTestHarness
    {
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
                });

            _serviceProvider = serviceCollection.BuildServiceProvider();

            HarmonyPatches.Apply();
        }

        public static PapyrusProgram CreateProgram(Action<ProgramOptionsBuilder> builderAction = null)
        {
            var optionsBuilder = new ProgramOptionsBuilder()
                .WithName("TestEnvironmentProgram")
                .WithFlagsFileName("TestFlags.flg")
                .WithSourceIncludes(new SourceInclude() { Path = "../../../scripts/Base" });

            builderAction?.Invoke(optionsBuilder);

            return CreateProgram(optionsBuilder.Build());
        }

        public static PapyrusProgram CreateProgram(ProgramOptions options)
        {
            return _serviceProvider.CreateInstance<PapyrusProgram>(options);
        }
    }
}
