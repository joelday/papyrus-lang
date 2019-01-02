using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using PCompiler;

namespace DarkId.Papyrus.LanguageService.Program.Syntax
{
    class ScopeBinder
    {
        public DiagnosticResult Bind(ScriptObjectType type, ScriptNode node)
        {
            return DiagnosticResult.TryWithDiagnostics((diagnostics) =>
            {
                var visitor = new ScopeBindingVisitor(diagnostics, type);
                visitor.Visit(node);
            });
        }
    }
}