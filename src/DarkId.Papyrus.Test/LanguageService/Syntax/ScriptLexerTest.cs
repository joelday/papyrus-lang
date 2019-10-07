using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptLexerTest : ProgramTestBase
    {
        public ScriptLexerTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void TokenizeTest()
        {
            var lexer = new ScriptLexer();
            var diagnostics = new DiagnosticsContext();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text;

            var tokens = lexer.Tokenize(
                scriptText,
                LanguageVersion,
                diagnostics);

            var tokenText = string.Empty;

            foreach (var token in tokens)
            {
                tokenText += token.Text;
                TestContext.Out.WriteLine(token.ToString());
            }

            Assert.AreEqual(scriptText.Text, tokenText, "Concatenated token texts should match the source text.");
        }
    }
}