using DarkId.Papyrus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public class ScriptParserDiagnostic : Diagnostic
    {
        public ScriptNode Node { get; }

        public ScriptParserDiagnostic(DiagnosticLevel severity, string message, ScriptNode node, Exception exception = null) :
            base(severity, message, node.Range, exception)
        {
            Node = node;
        }
    }
}
