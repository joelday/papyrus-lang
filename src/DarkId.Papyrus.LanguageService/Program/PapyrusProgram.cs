using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Types;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class PapyrusProgram : DisposableObject
    {
        private readonly ProgramOptions _options;
        private readonly IFileSystem _fileSystem;
        private readonly IScriptTextProvider _textProvider;

        private readonly ILogger _logger;
        private readonly ILogger<ScriptFile> _scriptFileLogger;

        private readonly FlagsFile _flagsFile;
        private readonly TypeChecker _typeChecker;

        // Internal storage class
        class SourceObject
        {
            public ObjectIdentifier Identifier;
            public string Path;
        }

        private readonly SourceCache<SourceObject, ObjectIdentifier> _objects = new SourceCache<SourceObject, ObjectIdentifier>(i => i.Identifier);
        public readonly IObservableCache<ScriptFile, ObjectIdentifier> ScriptFiles;
        public readonly IObservableCache<ObjectIdentifier, string> FilePaths;
        public readonly IObservableCache<ScriptFile, string> ScriptByPaths;
        public TypeChecker TypeChecker => _typeChecker;

        public string Name => _options.Name;
        public FlagsFile FlagsFile => _flagsFile;

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
            Add(_flagsFile);

            _typeChecker = new TypeChecker(this);

            var scriptFiles = _objects.Connect()
                .Transform(obj =>  new ScriptFile(obj.Identifier, obj.Path, this, _textProvider, _scriptFileLogger))
                .DisposeMany()
                .RefCount();
            ScriptFiles = scriptFiles
                .AsObservableCache(applyLocking: true);
            FilePaths = _objects.Connect()
                .ChangeKey(obj => obj.Path)
                .Transform(obj => obj.Identifier)
                .AsObservableCache(applyLocking: true);
            ScriptByPaths = scriptFiles
                .ChangeKey(obj => obj.FilePath)
                .AsObservableCache(applyLocking: true);
        }

        public Task<string> GetFlagsFilePath()
        {
            return _fileSystem.ResolveFlagsFile(_options);
        }

        public async Task<Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>>> ResolveSources()
        {
            var includes = await _fileSystem.ResolveSourceFileIncludes(_options.Sources);
            _objects.SetTo(includes.FlattenIncludes()
                .Select(i => new SourceObject()
                {
                    Identifier = i.Key,
                    Path = i.Value
                }));
            return includes;
        }

        public override void Dispose()
        {
            _objects.Clear();
        }
    }
}