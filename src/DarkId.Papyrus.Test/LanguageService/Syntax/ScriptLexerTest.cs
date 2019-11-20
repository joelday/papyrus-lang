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
                scriptText).ToLogicalLines().ToList();

            var tokenText = string.Empty;

            foreach (var token in tokens.SelectMany(n => n))
            {
                tokenText += token.FullText;
                TestContext.Out.WriteLine(token.ToString());
            }

            Assert.AreEqual(scriptText, tokenText, "Concatenated token texts should match the source text.");
        }
    }
}