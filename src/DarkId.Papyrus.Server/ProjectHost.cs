using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    public class ProjectHost : IDisposable
    {
        private readonly object _lock = new object();
        private readonly PapyrusProgram _program;
        private readonly ILogger _logger;
        private readonly string _name;
        private readonly ILanguageServer _languageServer;

        private readonly Dictionary<ObjectIdentifier, Debounce> _debouncedChangeHandlers =
            new Dictionary<ObjectIdentifier, Debounce>();

        public ProjectHost(ProgramOptions options, IServiceProvider serviceProvider, ILanguageServer languageServer, ILogger<ProjectHost> logger)
        {
            _logger = logger;
            _name = options.Name;
            _languageServer = languageServer;

            _program = serviceProvider.CreateInstance<PapyrusProgram>(options);
            _program.OnScriptFileChanged += HandleScriptFileChanged;
        }

        private void HandleScriptFileChanged(object sender, ScriptFileChangedEventArgs e)
        {
            _logger.LogDebug($"Script file changed: {e.ScriptFile.Id} ({e.ScriptFile.FilePath})");

            lock (_lock)
            {
                if (!_debouncedChangeHandlers.ContainsKey(e.ScriptFile.Id))
                {
                    _debouncedChangeHandlers.Add(e.ScriptFile.Id,
                        new Debounce(() => Task.Run(() =>
                        {
                            _program.ScriptFiles.TryGetValue(e.ScriptFile.Id, out var scriptFile);
                            if (scriptFile != null)
                            {
                                scriptFile.PublishDiagnostics(_languageServer.Document);
                            }
                        }), TimeSpan.FromSeconds(0.5)));
                }

                _debouncedChangeHandlers[e.ScriptFile.Id].Trigger();
            }
        }

        public string Name => _name;
        public Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>> Sources { get; private set; }

        public PapyrusProgram Program
        {
            get
            {
                lock (_lock)
                {
                    return _program;
                }
            }
        }

        public void ResolveSources()
        {
            lock (_lock)
            {
                _logger.LogInformation("Resolving script files for {0}...", Name);
                Sources = _program.ResolveSources().WaitForResult();

                _logger.LogInformation("Done");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var debounce in _debouncedChangeHandlers.Values)
                {
                    debounce.Dispose();
                }

                _program.OnScriptFileChanged -= HandleScriptFileChanged;
                _program.Dispose();
            }
        }
    }
}