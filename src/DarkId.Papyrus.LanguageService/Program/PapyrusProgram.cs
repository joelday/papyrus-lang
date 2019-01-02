using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr.Runtime;
using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Compiler;
using DarkId.Papyrus.LanguageService.Program.Types;
using Microsoft.Extensions.Logging;
using PCompiler;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class PapyrusProgram : IDisposable
    {
        private readonly object _lock = new object();

        private readonly ProgramOptions _options;
        private readonly IFileSystem _fileSystem;
        private readonly IScriptTextProvider _textProvider;

        private readonly ILogger _logger;
        private readonly ILogger<ScriptFile> _scriptFileLogger;

        private readonly FlagsFile _flagsFile;
        private readonly TypeChecker _typeChecker;

        private readonly Dictionary<ObjectIdentifier, ScriptFile> _scriptFiles
            = new Dictionary<ObjectIdentifier, ScriptFile>();

        private Dictionary<string, ObjectIdentifier> _filePaths
            = new Dictionary<string, ObjectIdentifier>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<ObjectIdentifier, ScriptFile> ScriptFiles => _scriptFiles;
        public IReadOnlyDictionary<string, ObjectIdentifier> FilePaths => _filePaths;
        public TypeChecker TypeChecker => _typeChecker;

        public string Name => _options.Name;
        public FlagsFile FlagsFile => _flagsFile;

        public event EventHandler<ScriptFileChangedEventArgs> OnScriptFileChanged;

        public PapyrusProgram(
            ProgramOptions options,
            IFileSystem fileSystem,
            IScriptTextProvider textProvider,
            ILogger<PapyrusProgram> logger,
            ILogger<ScriptFile> scriptFileLogger,
            ILogger<FlagsFile> flagsFileLogger)
        {
            _options = options.Clone();
            _fileSystem = fileSystem;
            _textProvider = textProvider;

            _logger = logger;
            _scriptFileLogger = scriptFileLogger;

            _flagsFile = new FlagsFile(this, textProvider, flagsFileLogger);

            _typeChecker = new TypeChecker(this);
        }

        private void RaiseScriptFileChanged(object sender, ScriptFileChangedEventArgs e)
        {
            OnScriptFileChanged?.Invoke(this, e);
        }

        public ScriptFile GetScriptForFilePath(string filePath)
        {
            lock (_lock)
            {
                // TODO: It's weird that both of these have to be checked given that they should always be in sync.
                return _filePaths.ContainsKey(filePath) && _scriptFiles.ContainsKey(_filePaths[filePath])
                    ? _scriptFiles[_filePaths[filePath]] : null;
            }
        }

        public Task<string> GetFlagsFilePath()
        {
            return _fileSystem.ResolveFlagsFile(_options);
        }

        public async Task ResolveSources()
        {
            var newFiles = await _fileSystem.ResolveSourceFiles(_options.Sources);

            lock (_lock)
            {
                var fileSyncTask = Task.Run(() =>
                {
                    _scriptFiles.SynchronizeWithFactory(
                        new HashSet<ObjectIdentifier>(newFiles.Keys),
                        (key) =>
                        {
                            var scriptFile = new ScriptFile(key, newFiles[key], this, _textProvider, _scriptFileLogger);
                            scriptFile.OnChanged += RaiseScriptFileChanged;
                            return scriptFile;
                        },
                        preDisposalHandler: (key, scriptFile) =>
                        {
                            scriptFile.OnChanged -= RaiseScriptFileChanged;
                        });
                });

                var filePathSyncTask = Task.Run(() =>
                {
                    _filePaths = newFiles.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
                });

                Task.WaitAll(fileSyncTask, filePathSyncTask);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _flagsFile.Dispose();
                _scriptFiles.SynchronizeWithFactory(new HashSet<ObjectIdentifier>(), _ => null);
            }
        }
    }
}