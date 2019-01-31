using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using CommandLine;
using System.Linq;
using System.IO;
using System.Reflection;

namespace DarkId.Papyrus.Server.Host
{
    public class Options
    {
        [Option("compilerAssemblyPath", Required = true)]
        public string CompilerAssemblyPath { get; set; }
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
                .WithParsed(o =>
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

                        var assemblyPath = Path.Combine(o.CompilerAssemblyPath, new AssemblyName(resolveArgs.Name).Name + ".dll");

                        var assembly = Assembly.LoadFile(assemblyPath);

                        if (assembly == null)
                        {
                            throw new Exception($"Failed to load Papyrus compiler assembly ${resolveArgs.Name} from ${o.CompilerAssemblyPath}.");
                        }

                        return assembly;
                    };

                    foreach (var assemblyName in _papyrusCompilerAssemblies)
                    {
                        AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(o.CompilerAssemblyPath, assemblyName + ".dll")));
                    }
                    
                    RunServer().Wait();
                });
        }

        static async Task RunServer()
        {
            var server = await PapyrusLanguageServer.From((options) => options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
#pragma warning disable CS0618 // Type or member is obsolete
                .WithLoggerFactory(new LoggerFactory().AddDebug())
#pragma warning restore CS0618 // Type or member is obsolete
                .AddDefaultLoggingProvider()
                .WithMinimumLogLevel(LogLevel.Trace));

            await server.WaitForExit;
        }
    }
}
