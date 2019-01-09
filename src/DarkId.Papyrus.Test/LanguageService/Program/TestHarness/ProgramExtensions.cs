using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DarkId.Papyrus.Test.LanguageService.Program.TestHarness
{
    public static class ProgramExtensions
    {
        public static Position GetTestMarker(this ScriptFile file, string marker)
        {
            return file.Text.PositionAt(file.Text.Text.IndexOf($";/marker:{marker}/;"));
        }

        public static void AssertAreOfKinds(this IEnumerable<PapyrusSymbol> symbols, SymbolKinds kinds)
        {
            Assert.IsTrue(symbols.All(
                s => (s.Kind & kinds) != 0),
                $"All symbols must be one of kinds {kinds}.");
        }
    }
}
