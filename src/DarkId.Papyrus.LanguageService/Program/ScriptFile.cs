using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Program.Types;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class ScriptFile : IDisposable
    {

        private readonly ObjectIdentifier _id;
        private readonly string _filePath;
        private readonly PapyrusProgram _program;
        private readonly IScriptTextProvider _textProvider;

        private readonly ILogger _logger;

        private readonly TokenEqualityCachedValue<ScriptText, string> _scriptText;

        public PapyrusProgram Program => _program;
        public ObjectIdentifier Id => _id;
        public string FilePath => _filePath;

        public event EventHandler<ScriptFileChangedEventArgs> OnChanged;

        public ScriptNode Node => throw new NotImplementedException();
        public ScriptSymbol Symbol => throw new NotImplementedException();
        public ScriptType Type => throw new NotImplementedException();

        public IEnumerable<Diagnostic> Diagnostics
        {
            get
            {
                //var diagnostics = _compilationResult.Value.Diagnostics.AsEnumerable();
                //if (_node.Value != null)
                //{
                //    diagnostics = diagnostics.Concat(_node.Value.Diagnostics);
                //}

                //return diagnostics;

                throw new NotImplementedException();
            }
        }

        public IReadOnlyScriptText Text => _scriptText.Value;

        public ScriptFile(ObjectIdentifier id, string filePath, PapyrusProgram program, IScriptTextProvider textProvider, ILogger<ScriptFile> logger)
        {
            _id = id;
            _filePath = filePath;
            _program = program;
            _textProvider = textProvider;
            _logger = logger;

            var initialVersion = _textProvider.GetTextVersion(_filePath).WaitForResult();

            _scriptText = new TokenEqualityCachedValue<ScriptText, string>(
                () => _textProvider.GetText(_filePath).WaitForResult(),
                (_) =>
                {
                    var currentVersion = _scriptText.CurrentToken;
                    var version = _textProvider.GetTextVersion(_filePath).WaitForResult();

                    if (currentVersion != version)
                    {
                        _logger.LogTrace($"{_id} file version changed from '{currentVersion}' to '{version}'. (Thread: {Thread.CurrentThread.ManagedThreadId})");
                    }

                    return version;
                },
                initialVersion);

            _textProvider.OnScriptTextChanged += HandleScriptTextChanged;
        }


        public ObjectIdentifier ResolveRelativeTypeName(string name)
        {
            //return UseCompiler((compiler, knownTypes) =>
            //{
            //    return compiler.DisambiguateType(name, null, CompilerType, knownTypes);
            //});

            throw new NotImplementedException();
        }

        private void RaiseScriptFileChanged()
        {
            OnChanged?.Invoke(this, new ScriptFileChangedEventArgs(this));
        }

        private void HandleScriptTextChanged(object sender, ScriptTextChangedEventArgs e)
        {
            if (e.ScriptText.FilePath.CaseInsensitiveEquals(_filePath))
            {
                //var types = _program.TypeChecker.CompilerTypeTable.Types;
                //var scriptName = Id.FullScriptName;

                //lock (types)
                //{
                //    // We need to remove any structs that are now invalidated:

                //    var dirtyKeys = types.Keys.Where(k =>
                //        ObjectIdentifier.Parse(k).FullScriptName.CaseInsensitiveEquals(scriptName)).ToArray();

                //    foreach (var key in dirtyKeys)
                //    {
                //        types.Remove(key);
                //    }
                //}

                RaiseScriptFileChanged();
            }
        }

        public string GetScriptAssembly()
        {
//            if (CompilerType == null)
//            {
//                return null;
//            }

//            var papyrusGen = new PapyrusGen(new CommonTreeNodeStream(CompilerType.GetAst())
//            {
//                TokenStream = CompilerType.GetTokenStream()
//            });

//            using (var manifestResourceStream = papyrusGen.GetType().Assembly.GetManifestResourceStream("PCompiler.PapyrusAssembly.stg"))
//            {
//                var templateGroup = new StringTemplateGroup(new StreamReader(manifestResourceStream));
//#if SKYRIM
//                papyrusGen.TemplateLib = templateGroup;
//#else
//                papyrusGen.TemplateGroup = templateGroup;
//#endif

//                papyrusGen.AsDynamic().KnownUserFlags = _program.FlagsFile.NativeFlagsDict;
//                var template = papyrusGen.script(_filePath, CompilerType).Template;
//                return template.ToString();
//            }

            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _textProvider.OnScriptTextChanged -= HandleScriptTextChanged;
        }
    }
}