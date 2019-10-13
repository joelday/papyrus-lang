using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptLexerTest : ProgramTestBase
    {
        public ScriptLexerTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void Tokenize_ProducesTokensFromSourceText()
        {
            var lexer = new ScriptLexer();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text;

            var tokens = lexer.Tokenize(
                scriptText);

            var tokenText = string.Empty;

            foreach (var token in tokens)
            {
                tokenText += token.Text;
                TestContext.Out.WriteLine(token.ToString());
            }

            Assert.AreEqual(scriptText.Text, tokenText, "Concatenated token texts should match the source text.");
        }

        private void Tokenize_CanResumeTokenizationWithOffset(int offset, List<ScriptToken> baselineTokens)
        {
            var lexer = new ScriptLexer();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text;

            var firstBatch = lexer.Tokenize(
                scriptText).Take(offset).ToList();

            var lastBatch = lexer.Tokenize(
                scriptText,
                firstBatch.Last()).ToList();

            var tokens = firstBatch.Concat(lastBatch).ToList();

            var tokenText = tokens.Aggregate(string.Empty, (current, token) => current + token.Text);

            Assert.AreEqual(scriptText.Text, tokenText, "Concatenated token texts should match the source text.");
            CollectionAssert.AreEqual(baselineTokens, tokens);
        }

        [Test]
        public void Tokenize_CanResumeTokenization()
        {
            var lexer = new ScriptLexer();

            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text;

            var tokens = lexer.Tokenize(
                scriptText).ToList();

            Enumerable.Range(1, tokens.Count - 2).AsParallel().ForAll((offset) => Tokenize_CanResumeTokenizationWithOffset(offset, tokens));
        }
    }
}