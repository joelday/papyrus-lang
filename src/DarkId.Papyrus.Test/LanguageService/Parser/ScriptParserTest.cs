using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptParserTest : PerLanguageProgramTestBase
    {
        public ScriptParserTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void Tokenize_ProducesTokensFromSourceText()
        {
            var lexer = new ScriptLexer();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text.Text;

            var tokens = lexer.Tokenize(
                scriptText);

            var parser = new ScriptParser();
            var node = parser.Parse(tokens);

            TestContext.Out.WriteLine(node.ToString());
        }
    }
}