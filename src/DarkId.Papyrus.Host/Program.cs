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
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    // https://stackoverflow.com/questions/1373100/how-to-add-folder-to-assembly-search-path-at-runtime-in-net

                    AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs resolveArgs) =>
                    {
                        if (!resolveArgs.Name.StartsWith("Antlr", StringComparison.Ordinal) && !resolveArgs.Name.StartsWith("PCompiler", StringComparison.Ordinal))
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

                        var assemblyPath = Path.Combine(o.CompilerAssemblyPath, new AssemblyName(resolveArgs.Name).Name, ".dll");

                        var assembly = Assembly.LoadFile(assemblyPath);

                        if (assembly == null)
                        {
                            throw new Exception($"Failed to load Papyrus compiler assembly ${resolveArgs.Name} from ${o.CompilerAssemblyPath}.");
                        }

                        return assembly;
                    };

                    AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(o.CompilerAssemblyPath, "Antlr3.Runtime.dll")));
                    AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(o.CompilerAssemblyPath, "Antlr3.StringTemplate.dll")));
                    AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(o.CompilerAssemblyPath, "PCompiler.dll")));

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
