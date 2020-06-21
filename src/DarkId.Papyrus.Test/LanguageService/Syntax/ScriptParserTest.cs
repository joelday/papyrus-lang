using System.Collections.Generic;
using System;
using System.Linq;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;
using DarkId.Papyrus.LanguageService.Syntax.Parser;
using NUnit.Framework;
using SyntaxExtensions = DarkId.Papyrus.LanguageService.Syntax.SyntaxExtensions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DarkId.Papyrus.Test.LanguageService.Syntax
{
    public class ScriptParserTest : PerLanguageProgramTestBase
    {
        [Test]
        public void Parser_ParsesScripts()
        {
            TestContext.Error.WriteLine($"Language Version: {LanguageVersion}");

            var scriptText = Program.ScriptFiles.Lookup(ObjectIdentifier.Parse("WorkshopParentScript")).Value.Text.Text;

            var watch = new Stopwatch();
            watch.Start();
            var parser = new ScriptParser();
            var script = parser.Parse(scriptText, LanguageVersion);
            watch.Stop();

            TestContext.Error.WriteLine(watch.Elapsed.ToString());

            Assert.IsEmpty(script.EnumerateDescendants().SelectMany(n => n.Diagnostics));

            TestContext.Error.WriteLine();
            TestContext.Error.WriteLine();
        }

        [Test]
        public void Parser_IsStableWhenParsingPartialText()
        {
            TestContext.Error.WriteLine($"Language Version: {LanguageVersion}");

            var scriptText = Program.ScriptFiles.Lookup(ObjectIdentifier.Parse("WorkshopParentScript")).Value.Text.Text;

            Console.WriteLine(System.Environment.ProcessorCount);

            Enumerable.Range(0, scriptText.Length).Where(length => length % 2000 == 0).
                AsParallel().
                AsUnordered().
                WithExecutionMode(ParallelExecutionMode.ForceParallelism).
                WithDegreeOfParallelism(16).
                ForAll(length =>
            {
                var parser = new ScriptParser();
                parser.Parse(scriptText.Substring(0, length), LanguageVersion);
            });
        }
    }
}