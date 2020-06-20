using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Types;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class PapyrusProgram : DisposableObject
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

        private readonly Subject<ScriptFile> _scriptFileChanged = new Subject<ScriptFile>();
        public IObservable<ScriptFile> ScriptFileChanged => _scriptFileChanged;

        public ProgramOptions Options => _options.Clone();

        public PapyrusProgram(
            ProgramOptions options,
            IFileSystem fileSystem,
            IScriptTextProvider textProvider,
            ILogger<PapyrusProgram> logger,
            ILogger<ScriptFile> scriptFileLogger,
            ILogger<FlagsFile> flagsFileLogger)
        {
            _options = options.Clone();

            if (_options.LanguageVersion != LanguageVersion.Fallout4 && _options.LanguageVersion != LanguageVersion.Skyrim)
            {
                throw new InvalidOperationException("Invalid language version.");
            }

            _fileSystem = fileSystem;
            _textProvider = textProvider;

            _logger = logger;
            _scriptFileLogger = scriptFileLogger;

            _flagsFile = new FlagsFile(this, textProvider, flagsFileLogger);

            _typeChecker = new TypeChecker(this);
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

        public async Task<Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>>> ResolveSources()
        {
            var includes = await _fileSystem.ResolveSourceFileIncludes(_options.Sources);
            var newFiles = includes.FlattenIncludes();

            lock (_lock)
            {
                var fileSyncTask = Task.Run(() =>
                {
                    _scriptFiles.SynchronizeWithFactory(
                        new HashSet<ObjectIdentifier>(newFiles.Keys),
                        (key) =>
                        {
                            var scriptFile = new ScriptFile(key, newFiles[key], this, _textProvider, _scriptFileLogger);
                            scriptFile.Add(scriptFile.Changed.Subscribe(_ => _scriptFileChanged.OnNext(scriptFile)));
                            return scriptFile;
                        });
                });

                var filePathSyncTask = Task.Run(() =>
                {
                    _filePaths = newFiles.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
                });

                Task.WaitAll(fileSyncTask, filePathSyncTask);
            }

            return includes;
        }

        public override void Dispose()
        {
            lock (_lock)
            {
                _flagsFile.Dispose();
                _scriptFiles.SynchronizeWithFactory(new HashSet<ObjectIdentifier>(), _ => null);
            }
        }
    }
}