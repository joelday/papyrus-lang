using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    [TestFixture]
    public class ScriptFileTests : ProgramTestBase
    {
        // TODO: Failure case tests.

        [Test]
        public void ScriptFile_ShouldNotProduceDiagnostics()
        {
            var allDiagnostics = Program.ScriptFiles.Values.AsParallel().AsOrdered().
                Where(s => s.Text.Text.IndexOf(";/test:ignore-diagnostics/;", StringComparison.Ordinal) == -1).SelectMany(s => s.Diagnostics).ToList();

            Console.WriteLine(allDiagnostics.Select(d => d.Message).Join(",\r\n\r\n"));

            Assert.AreEqual(0, allDiagnostics.Count);
        }

        [Test]
        public void ScriptFile_GetAssembly_ShouldReturnAssembly()
        {
            var allAsm = Program.ScriptFiles.Values.AsParallel().AsOrdered().
                Where(s => s.Text.Text.IndexOf(";/test:ignore-diagnostics/;", StringComparison.Ordinal) == -1).Select(s => s.GetScriptAssembly()).ToList();

            Console.WriteLine(allAsm.Join(",\r\n\r\n"));
        }
    }
}

