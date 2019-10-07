using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService
{
    public static class LanguageServiceExtensions
    {
        public static TextPosition GetTestMarker(this ScriptFile file, string marker, bool beforeMarker = false)
        {
            var markerComment = $";/marker:{marker}/;";
            var markerCommentIndex = file.Text.Text.IndexOf(markerComment);
            if (markerCommentIndex == -1)
            {
                return new TextPosition();
            }

            return file.Text.PositionAt(markerCommentIndex + (beforeMarker ? 0 : markerComment.Length));
        }

        public static SyntaxNode GetNodeAtMarker(this ScriptFile file, string marker, bool beforeMarker = false)
        {
            var markerPosition = file.GetTestMarker(marker, beforeMarker);
            return file.Node.GetNodeAtPosition(markerPosition);
        }

        public static T GetNodeAtMarker<T>(this ScriptFile file, string marker, bool beforeMarker = false) where T : SyntaxNode
        {
            var markerPosition = file.GetTestMarker(marker, beforeMarker);
            return file.Node.GetDescendantNodeOfTypeAtPosition<T>(markerPosition);
        }


        public static void AssertAreOfKinds(this IEnumerable<PapyrusSymbol> symbols, SymbolKinds kinds)
        {
            Assert.IsTrue(symbols.All(
                s => (s.Kind & kinds) != 0),
                $"All symbols must be one of kinds {kinds}.");
        }
    }
}
