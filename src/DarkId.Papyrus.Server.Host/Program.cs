using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using CommandLine;
using System.Linq;
using System.IO;
using System.Reflection;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;

namespace DarkId.Papyrus.Server.Host
{
    public class Options
    {
        [Option("languageVersion", Required = true)]
        public LanguageVersion LanguageVersion { get; set; }

        [Option("flagsFileName", Required = true)]
        public string FlagsFileName { get; set; }

        [Option("ambientProjectName", Required = true)]
        public string AmbientProjectName { get; set; }

        [Option("defaultScriptSourceFolder")]
        public string DefaultScriptSourceFolder { get; set; }

        [Option("defaultAdditionalImports")]
        public string DefaultAdditionalImports { get; set; }

        [Option("creationKitInstallPath", Required = true)]
        public string CreationKitInstallPath { get; set; }

        [Option("relativeIniPaths", Required = true)]
        public IEnumerable<string> RelativeIniPaths { get; set; }
    }

    public class Program
    {

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    RunServer(options).Wait();
                });
        }

        static async Task RunServer(Options options)
        {
            var server = await PapyrusLanguageServer.From((serverOptions, papyrusOptions) =>
            {
                papyrusOptions.LanguageVersion = options.LanguageVersion;
                papyrusOptions.AmbientProjectName = options.AmbientProjectName;
                papyrusOptions.DefaultCreationKitConfig = new CreationKitConfig()
                {
                    Papyrus = new CreationKitPapyrusConfig()
                    {
                        sScriptSourceFolder = options.DefaultScriptSourceFolder,
                        sAdditionalImports = options.DefaultAdditionalImports
                    }
                };
                papyrusOptions.FlagsFileName = options.FlagsFileName;
                papyrusOptions.IniLocations = new CreationKitIniLocations()
                {
                    CreationKitInstallPath = options.CreationKitInstallPath,
                    RelativeIniPaths = options.RelativeIniPaths.ToList()
                };

                serverOptions
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .WithLoggerFactory(LoggerFactory.Create((builder) => builder.AddDebug()))
                    .AddDefaultLoggingProvider()
                    .WithMinimumLogLevel(LogLevel.Trace);
            });

            await server.WaitForExit;
        }
    }
}
