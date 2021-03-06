using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr.Runtime;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.External;
using Microsoft.Extensions.Logging;
using PCompiler;
using ReflectionMagic;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class FlagsFile : IDisposable
    {
        private readonly PapyrusProgram _program;
        private readonly IScriptTextProvider _textProvider;
        private readonly ILogger _logger;

        private readonly CachedValue<string> _flagsFilePath;
        private readonly TokenEqualityCachedValue<ScriptText, string> _scriptText;
        private readonly TokenEqualityCachedValue<DiagnosticResult<dynamic>, string> _parseResult;

        public event EventHandler<FlagsFileChangedEventArgs> OnChanged;

        public dynamic NativeFlagsDict => _parseResult.Value.Value;
        public IReadOnlyList<Diagnostic> Diagnostics => _parseResult.Value.Diagnostics;
        public IReadOnlyScriptText Text => _scriptText.Value;

        public FlagsFile(PapyrusProgram program, IScriptTextProvider textProvider, ILogger<FlagsFile> logger)
        {
            _program = program;
            _textProvider = textProvider;

            _logger = logger;

            // TODO: Invalidate this
            _flagsFilePath = new CachedValue<string>(() => _program.GetFlagsFilePath().WaitForResult());

            _scriptText = new TokenEqualityCachedValue<ScriptText, string>(
                () => _textProvider.GetText(_flagsFilePath).WaitForResult(),
                (_) => _textProvider.GetTextVersion(_flagsFilePath).WaitForResult());

            _textProvider.OnScriptTextChanged += HandleScriptTextChanged;

            _parseResult = new TokenEqualityCachedValue<DiagnosticResult<dynamic>, string>(() =>
            {
                return DiagnosticResult<dynamic>.TryWithDiagnostics((diagnostics) =>
                {
                    void errorHandler(object s, ErrorEventArgs e)
                    {
                        diagnostics.Add(e.ToDiagnostic());
                    }

                    var flagsLexer = new FlagsLexer(new CaseInsensitiveStringStream(_scriptText.Value.Text));
                    flagsLexer.OnError(errorHandler);

                    var flagsParser = new FlagsParser(new CommonTokenStream(flagsLexer));
                    flagsParser.OnError(errorHandler);

                    flagsParser.flags();

                    return flagsParser.AsDynamic().DefinedFlags;
                });
            },
            (_) => _scriptText.Value.Version);
        }

        private void RaiseFlagsFileChanged()
        {
            OnChanged?.Invoke(this, new FlagsFileChangedEventArgs(this));
        }

        private void HandleScriptTextChanged(object sender, ScriptTextChangedEventArgs e)
        {
            if (e.ScriptText.FilePath.CaseInsensitiveEquals(_program.GetFlagsFilePath().WaitForResult()))
            {
                RaiseFlagsFileChanged();
            }
        }

        public void Dispose()
        {
            _textProvider.OnScriptTextChanged -= HandleScriptTextChanged;
        }
    }
}