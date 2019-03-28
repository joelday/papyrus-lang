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
        public static Position GetTestMarker(this ScriptFile file, string marker, bool before = false)
        {
            var markerComment = $";/marker:{marker}/;";
            var markerCommentIndex = file.Text.Text.IndexOf(markerComment);
            if (markerCommentIndex == -1)
            {
                return new Position();
            }

            return file.Text.PositionAt(markerCommentIndex + (before ? 0 : markerComment.Length));
        }

        public static void AssertAreOfKinds(this IEnumerable<PapyrusSymbol> symbols, SymbolKinds kinds)
        {
            Assert.IsTrue(symbols.All(
                s => (s.Kind & kinds) != 0),
                $"All symbols must be one of kinds {kinds}.");
        }
    }
}
