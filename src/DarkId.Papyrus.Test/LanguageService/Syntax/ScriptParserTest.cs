using System.Collections.Generic;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using DarkId.Papyrus.LanguageService.Syntax.Parser;
using NUnit.Framework;
using SyntaxExtensions = DarkId.Papyrus.LanguageService.Syntax.SyntaxExtensions;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptParserTest : PerLanguageProgramTestBase
    {
        public ScriptParserTest(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public void Parser_ParsesScripts()
        {
            var scriptText = Program.ScriptFiles[ObjectIdentifier.Parse("LineContinuations")].Text.Text;

            var parser = new ScriptParser();
            var script = parser.Parse(scriptText, LanguageVersion.Fallout4);

            TestContext.Out.Write(script.PrintTree());

            foreach (var node in script.EnumerateDescendants())
            {
                foreach (var diagnostic in node.Diagnostics)
                {
                    TestContext.Out.WriteLine(node.Text + " (" + diagnostic.Message + ")");
                }
            }

            Assert.IsEmpty(script.EnumerateDescendants().SelectMany(n => n.Diagnostics));

            TestContext.Out.WriteLine();
            TestContext.Out.Write(script.PrintTree());
        }

    }
}