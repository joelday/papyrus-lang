// TODO: Remove usage of deprecated AddDebug extension method.
#pragma warning disable CS0618 // Type or member is obsolete

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PureWebSockets;
using CommandLine;
using System.Globalization;
using Newtonsoft.Json;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Projects;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Newtonsoft.Json.Linq;

namespace DarkId.Papyrus.DebugAdapterProxy
{
    public class Options
    {
        [Option("port")]
        public int Port { get; set; } = 2700;

        [Option("projectPath")]
        public string ProjectPath { get; set; }

        [Option("defaultScriptSourceFolder")]
        public string DefaultScriptSourceFolder { get; set; }

        [Option("defaultAdditionalImports")]
        public string DefaultAdditionalImports { get; set; }

        [Option("creationKitInstallPath")]
        public string CreationKitInstallPath { get; set; }

        [Option("relativeIniPaths")]
        public IEnumerable<string> RelativeIniPaths { get; set; } = new List<string>();
    }

    class Program
    {
        static ILoggerFactory loggerFactory;
        static ILogger<Program> logger;

        static int Main(string[] args)
        {
            loggerFactory = new LoggerFactory()
                .AddDebug(LogLevel.Trace)
                .AddFile(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "My Games\\Fallout4\\F4SE\\papyrus_debug_proxy.log"),
                    LogLevel.Trace);

            logger = loggerFactory.CreateLogger<Program>();

            int exitCode = 0;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    exitCode = RunWithSources(ResolveSources(options).WaitForResult(), options.Port);
                })
                .WithNotParsed((error) =>
                {
                    exitCode = 1;
                });

            loggerFactory.Dispose();

            return exitCode;
        }

        static async Task<Dictionary<ObjectIdentifier, string>> ResolveSources(Options options)
        {
            var fileSystem = new LocalFileSystem();

            if (!string.IsNullOrEmpty(options.ProjectPath))
            {
                var projectDeserializer = new XmlProjectDeserializer();
                var projectLoader = new FileSystemXmlProjectLoader(fileSystem, projectDeserializer);
                var project = await projectLoader.LoadProject(options.ProjectPath);

                var programOptions = new ProgramOptionsBuilder().WithProject(project).Build();
                return await fileSystem.ResolveSourceFiles(programOptions.Sources);
            }
            else if (!string.IsNullOrEmpty(options.DefaultScriptSourceFolder) && !string.IsNullOrEmpty(options.DefaultAdditionalImports))
            {
                var creationKitConfig = new CreationKitConfig()
                {
                    Papyrus = new CreationKitPapyrusConfig()
                    {
                        sScriptSourceFolder = options.DefaultScriptSourceFolder,
                        sAdditionalImports = options.DefaultAdditionalImports
                    }
                };

                var configLoader = new CreationKitInisConfigLoader();

                var inisLocations = new CreationKitIniLocations()
                {
                    CreationKitInstallPath = options.CreationKitInstallPath,
                    RelativeIniPaths = options.RelativeIniPaths.ToList()
                };

                var inisLocator = new CreationKitInisLocator(inisLocations);

                var programOptionsProvider = new CreationKitProgramOptionsProvider(
                    string.Empty,
                    string.Empty,
                    creationKitConfig,
                    inisLocator,
                    configLoader,
                    loggerFactory.CreateLogger<CreationKitProgramOptionsProvider>());

                var programOptions = programOptionsProvider.GetAmbientProgramOptions();
                return await fileSystem.ResolveSourceFiles(programOptions.Sources);
            }

            return new Dictionary<ObjectIdentifier, string>();
        }

        static int RunWithSources(Dictionary<ObjectIdentifier, string> sources, int port)
        {
            var outputStream = Console.OpenStandardOutput();

            var textWriter = new StringWriter();

            var client = new PureWebSocket(string.Format("ws://localhost:{0}", port), new PureWebSocketOptions());

            client.OnError += (e) =>
            {
                logger.LogError(e, "Socket error");
            };

            client.OnMessage += (message) =>
            {
                logger.LogTrace("Forwarding message from server: {0}", message);

                lock (outputStream)
                {
                    try
                    {
                        var root = JObject.Parse(message);

                        WalkNode(root, (obj) =>
                        {
                            if (obj["sourceReference"] != null && obj["name"] != null && sources.ContainsKey(obj["name"].ToString()))
                            {
                                obj["sourceReference"] = 0;
                                obj["path"] = sources[obj["name"].ToString()];
                            }
                        });

                        message = root.ToString(Formatting.None);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception while inspecting or updating message content.");
                    }

                    try
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(message);
                        string header = string.Format(CultureInfo.InvariantCulture, "Content-Length: {0}\r\n\r\n", messageBytes.Length);
                        var headerBytes = Encoding.UTF8.GetBytes(header);

                        outputStream.Write(headerBytes, 0, headerBytes.Length);
                        outputStream.Write(messageBytes, 0, messageBytes.Length);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception thrown while forwarding message to client.");
                    }
                }
            };

            client.OnClosed += (reason) =>
            {
                logger.LogInformation("Connection closed: {0}", reason);
            };

            try
            {
                if (client.Connect())
                {
                    try
                    {
                        RunReadLoop(client);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception thrown inside client message read loop.");
                        return 1;
                    }
                }
                else
                {
                    throw new Exception("Connect call returned 'false'.");
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception thrown on connection attempt.");
                return 1;
            }

            return 0;
        }

        static void RunReadLoop(PureWebSocket client)
        {
            // TODO: Refactor to read from stream.
            while (client.State == System.Net.WebSockets.WebSocketState.Open)
            {
                var headers = new Dictionary<string, string>();
                while (true)
                {
                    var headerInput = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerInput))
                    {
                        break;
                    }

                    var headerPair = headerInput.Split(':');
                    headers.Add(headerPair[0].Trim(), headerPair[1].Trim());
                }

                var contentLength = int.Parse(headers["Content-Length"]);
                var message = new char[contentLength];

                for (var i = 0; i < contentLength; i++)
                {
                    message[i] = (char)Console.Read();
                }

                var completedMessage = new string(message);

                logger.LogTrace("Forwarding message from client: {0}", completedMessage);

                client.Send(completedMessage);
            }
        }

        // https://stackoverflow.com/a/20102554/146765
        private static void WalkNode(JToken node, Action<JObject> objectAction = null, Action<JProperty> propertyAction = null)
        {
            if (node.Type == JTokenType.Object)
            {
                objectAction?.Invoke((JObject)node);

                foreach (var child in node.Children<JProperty>())
                {
                    propertyAction?.Invoke(child);
                    WalkNode(child.Value, objectAction, propertyAction);
                }
            }
            else if (node.Type == JTokenType.Array)
            {
                foreach (var child in node.Children())
                {
                    WalkNode(child, objectAction, propertyAction);
                }
            }
        }
    }
}
