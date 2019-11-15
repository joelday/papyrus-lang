using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptParserTest : ProgramTestBase
    {
        public ScriptParserTest() : base(new TestServiceInstance(LanguageVersion.Fallout4).CreateProgram())
        {
        }

        [Test]
        public void Parser_ParsesScriptsFromTokens()
        {
            var lexer = new ScriptLexer();
            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text.Text;

            var tokens = lexer.Tokenize(scriptText);

            var parser = new ScriptParser();
            var node = parser.Parse(tokens, LanguageVersion.Fallout4);

        }
    }
}