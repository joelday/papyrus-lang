using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DynamicData;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    public class ProjectHost : DisposableObject
    {
        public PapyrusProgram Program { get; }
        private readonly ILogger _logger;
        private readonly ILanguageServer _languageServer;
        public string Name { get; }
        public Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>> Sources { get; private set; }

        public ProjectHost(ProgramOptions options, IServiceProvider serviceProvider, ILanguageServer languageServer, ILogger<ProjectHost> logger)
        {
            _logger = logger;
            Name = options.Name;
            _languageServer = languageServer;

            Program = serviceProvider.CreateInstance<PapyrusProgram>(options);
            Add(Program);
            Add(Program.ScriptFiles.Connect()
                .Transform(file =>
                {
                    return file.Changed
                        .Do(_ => _logger.LogDebug($"Script file changed: {file.Id} ({file.FilePath})"))
                        .Debounce(TimeSpan.FromSeconds(0.5d), Scheduler.Default)
                        .Do(_ =>
                        {
                            file.PublishDiagnostics(_languageServer.Document);
                        });
                })
                .MergeMany(i => i)
                .Subscribe());
        }

        public void ResolveSources()
        {
            _logger.LogInformation("Resolving script files for {0}...", Name);
            Sources = Program.ResolveSources().WaitForResult();
            _logger.LogInformation("Done");
        }
    }
}