using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using DarkId.Papyrus.LanguageService.Syntax.Parser;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptParserTest : PerLanguageProgramTestBase
    {
        public ScriptParserTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void Parser_ParsesScriptsFromTokens()
        {
            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text.Text;

            var parser = new ScriptParser();
            var node = parser.Parse(scriptText, LanguageVersion.Fallout4);

            // TODO: Traversed
            Assert.IsEmpty(node.Diagnostics);
        }

    }
}