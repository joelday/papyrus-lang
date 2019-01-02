using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Program.Syntax;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    class SymbolBinder
    {
        public DiagnosticResult<ScriptSymbol> Bind(ScriptNode node)
        {
            return DiagnosticResult<ScriptSymbol>.TryWithDiagnostics((diagnostics) =>
            {
                var visitor = new SymbolBindingVisitor(diagnostics);
                return (ScriptSymbol)visitor.Visit(node, null);
            });
        }
    }
}
