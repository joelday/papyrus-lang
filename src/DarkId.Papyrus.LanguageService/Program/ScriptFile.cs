using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Antlr.Runtime.Tree;
using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Compiler;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.LanguageService.Program.Types;
using Microsoft.Extensions.Logging;
using PCompiler;
using ReflectionMagic;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class ScriptFile : IDisposable
    {
        class CompilationResult
        {
            public ScriptObjectType CompilerType { get; set; }
            public CommonTree CompilerNode { get; set; }
        }

        private readonly ObjectIdentifier _id;
        private readonly string _filePath;
        private readonly PapyrusProgram _program;
        private readonly IScriptTextProvider _textProvider;

        private readonly ILogger _logger;

        private readonly TokenEqualityCachedValue<ScriptText, string> _scriptText;
        private readonly TokenEqualityCachedValue<ScriptCompiler, string> _compiler;
        private readonly TokenEqualityCachedValue<DiagnosticResult<CompilationResult>, ScriptCompiler> _compilationResult;

        private readonly TokenEqualityCachedValue<DiagnosticResult<ScriptNode>, CompilationResult> _node;
        private readonly TokenEqualityCachedValue<ScriptType, ScriptSymbol> _type;

        internal ScriptObjectType CompilerType => _compilationResult.Value.Value?.CompilerType;
        internal CommonTree CompilerNode => _compilationResult.Value.Value?.CompilerNode;

        public PapyrusProgram Program => _program;
        public ObjectIdentifier Id => _id;
        public string FilePath => _filePath;

        public event EventHandler<ScriptFileChangedEventArgs> OnChanged;

        public ScriptNode Node => _node.Value?.Value;
        public ScriptSymbol Symbol => _node.Value?.Value.Symbol;
        public ScriptType Type => _type.Value;

        public IEnumerable<Diagnostic> Diagnostics
        {
            get
            {
                var diagnostics = _compilationResult.Value.Diagnostics.AsEnumerable();
                if (_node.Value != null)
                {
                    diagnostics = diagnostics.Concat(_node.Value.Diagnostics);
                }
                
                return diagnostics;
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

            _compiler = new TokenEqualityCachedValue<ScriptCompiler, string>(() =>
            {
                var compiler = new ScriptCompiler(this, _logger);
                compiler.CompilerNotifyHandler += (s, e) =>
                {
                    _logger.LogTrace($"Compiler: {e.sMessage}");
                };

                return compiler;
            }, (_) => _scriptText.Value.Version, initialVersion);

            _compilationResult = new TokenEqualityCachedValue<DiagnosticResult<CompilationResult>, ScriptCompiler>(() =>
            {
                DiagnosticResult<CompilationResult> compileFunc()
                {
                    return DiagnosticResult<CompilationResult>.TryWithDiagnostics((diagnostics) =>
                    {
                        return UseCompiler((compiler, types) =>
                        {
                            var compilationResult = new CompilationResult();

                            _logger.LogTrace($"Compiling {_id}... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                            var type = compiler.Load(types);
                            compilationResult.CompilerType = type;
                            compilationResult.CompilerNode = type?.pObjAST;

                            _logger.LogTrace($"Type checking {_id}... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                            var typeWalker = new TypeWalker(this, type, _logger);
                            typeWalker.OnError((s, e) =>
                            {
                                diagnostics.Add(e.ToDiagnostic());
                            });

                            try
                            {
                                typeWalker.script(type, compiler, types);
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, $"Thrown during type checking of {_id}");
                            }

                            return compilationResult;
                        },
                        (s, e) =>
                        {
                            diagnostics.Add(e.ToDiagnostic());
                        });
                    });
                }

                // If there are diagnostic errors, we re-run the compile once in case anything was missing,
                // but couldn't be resolved at the time.
                var result = compileFunc();
                if (result.Diagnostics.Count > 0)
                {
                    return compileFunc();
                }

                return result;
            }, (_) => _compiler.Value);

            _node = new TokenEqualityCachedValue<DiagnosticResult<ScriptNode>, CompilationResult>(() =>
            {
                if (CompilerType == null)
                {
                    return null;
                }

                _logger.LogTrace($"Transforming {_id} syntax tree... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                var nodeBinder = new NodeBinder();

                var node = nodeBinder.Bind(this, _program, Text, CompilerType.pObjTokenStream, CompilerNode);

                _logger.LogTrace($"Binding script scopes to {_id} syntax tree... (Thread: {Thread.CurrentThread.ManagedThreadId})");
                var scopeBinder = new ScopeBinder();
                var scopeResult = scopeBinder.Bind(CompilerType, node.Value);

                node.Diagnostics.AddRange(scopeResult.Diagnostics);

                _logger.LogTrace($"Binding {_id} symbols... (Thread: {Thread.CurrentThread.ManagedThreadId})");
                var symbolBinder = new SymbolBinder();
                var symbolResult = symbolBinder.Bind(node.Value);

                node.Diagnostics.AddRange(symbolResult.Diagnostics);

                return node;
            }, (_) => _compilationResult.Value?.Value);

            _type = new TokenEqualityCachedValue<ScriptType, ScriptSymbol>(() =>
            {
                if (Symbol == null)
                {
                    return null;
                }

                _logger.LogTrace($"Creating {_id} type... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                return new ScriptType(Program, Symbol, CompilerType);
            }, (_) => Symbol);
        }

        private T UseCompiler<T>(Func<ScriptCompiler, Dictionary<string, ScriptComplexType>, T> action, CompilerErrorEventHandler errorHandler = null)
        {
            var compiler = _compiler.Value;
            lock (compiler)
            {
                try
                {
                    if (errorHandler != null)
                    {
                        compiler.CompilerErrorHandler += errorHandler;
                    }

                    return action(compiler, _program.TypeChecker.CompilerTypeTable.Types);
                }
                finally
                {
                    if (errorHandler != null)
                    {
                        compiler.CompilerErrorHandler -= errorHandler;
                    }
                }
            }
        }

        public ObjectIdentifier ResolveRelativeTypeName(string name)
        {
            return UseCompiler((compiler, knownTypes) =>
            {
                return compiler.DisambiguateType(name, null, CompilerType, knownTypes);
            });
        }

        private void RaiseScriptFileChanged()
        {
            OnChanged?.Invoke(this, new ScriptFileChangedEventArgs(this));
        }

        private void HandleScriptTextChanged(object sender, ScriptTextChangedEventArgs e)
        {
            if (e.ScriptText.FilePath.CaseInsensitiveEquals(_filePath))
            {
                var types = _program.TypeChecker.CompilerTypeTable.Types;
                var scriptName = Id.FullScriptName;

                lock (types)
                {
                    // We need to remove any structs that are now invalidated:

                    var dirtyKeys = types.Keys.Where(k =>
                        ObjectIdentifier.Parse(k).FullScriptName.CaseInsensitiveEquals(scriptName)).ToArray();

                    foreach (var key in dirtyKeys)
                    {
                        types.Remove(key);
                    }
                }

                RaiseScriptFileChanged();
            }
        }

        public void Dispose()
        {
            _textProvider.OnScriptTextChanged -= HandleScriptTextChanged;
        }
    }
}