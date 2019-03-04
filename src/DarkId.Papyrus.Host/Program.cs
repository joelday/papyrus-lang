using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using CommandLine;
using System.Linq;
using System.IO;
using System.Reflection;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;

namespace DarkId.Papyrus.Server.Host
{
    public class Options
    {
        [Option("compilerAssemblyPath", Required = true)]
        public string CompilerAssemblyPath { get; set; }

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
        private static readonly string[] _papyrusCompilerAssemblies = new string[]
        {
            "Antlr3.Runtime",
            "PCompiler",
#if FALLOUT4
            "Antlr3.StringTemplate"
#elif SKYRIM
            "Antlr3.Utility",
            "StringTemplate"
#endif
        };

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    // https://stackoverflow.com/questions/1373100/how-to-add-folder-to-assembly-search-path-at-runtime-in-net

                    AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs resolveArgs) =>
                    {
                        if (!_papyrusCompilerAssemblies.Any(a => resolveArgs.Name.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                        {
                            return null;
                        }

                        var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == resolveArgs.Name);
                        if (loadedAssembly != null)
                        {
                            return loadedAssembly;
                        }

                        if (resolveArgs.RequestingAssembly == null)
                        {
                            return null;
                        }

                        var assemblyPath = Path.Combine(options.CompilerAssemblyPath, new AssemblyName(resolveArgs.Name).Name + ".dll");

                        var assembly = Assembly.LoadFile(assemblyPath);
                        if (assembly == null)
                        {
                            throw new Exception($"Failed to load Papyrus compiler assembly ${resolveArgs.Name} from ${options.CompilerAssemblyPath}.");
                        }

                        return assembly;
                    };

                    foreach (var assemblyName in _papyrusCompilerAssemblies)
                    {
                        AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(options.CompilerAssemblyPath, assemblyName + ".dll")));
                    }

                    RunServer(options).Wait();
                });
        }

        static async Task RunServer(Options options)
        {
            var server = await PapyrusLanguageServer.From((serverOptions, papyrusOptions) =>
            {
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
#pragma warning disable CS0618 // Type or member is obsolete
                    .WithLoggerFactory(new LoggerFactory().AddDebug())
#pragma warning restore CS0618 // Type or member is obsolete
                    .AddDefaultLoggingProvider()
                    .WithMinimumLogLevel(LogLevel.Trace);
            });

            await server.WaitForExit;
        }
    }
}
