using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Syntax;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    class SymbolBinder
    {
        public ScriptSymbol Bind(ScriptNode node, DiagnosticsContext diagnostics)
        {
            throw new NotImplementedException();

            //var visitor = new SymbolBindingVisitor(diagnostics);
            //return (ScriptSymbol)visitor.Visit(node, null);
        }
    }
}
