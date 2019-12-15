using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;

using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptLexerTest : PerLanguageProgramTestBase
    {
        public ScriptLexerTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void Tokenize_ProducesTokensFromSourceText()
        {
            var lexer = new ScriptLexer();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text.Text;

            var tokens = lexer.Tokenize(
                scriptText).ToList();

            var tokenText = tokens.Aggregate(string.Empty, (s, t) => s + t.FullText);

            Assert.AreEqual(scriptText, tokenText, "Concatenated token texts should match the source text.");
        }
    }
}