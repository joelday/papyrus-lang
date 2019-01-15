using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    [TestClass]
    public class ScriptFileTests : ProgramTestBase
    {
        // TODO: Failure case tests.

        [TestMethod]
        public void ScriptFile_ShouldNotProduceDiagnostics()
        {
            var allDiagnostics = Program.ScriptFiles.Values.AsParallel().AsOrdered().SelectMany(s => s.Diagnostics).ToList();

            Console.WriteLine(allDiagnostics.Select(d => d.Message).Join(",\r\n\r\n"));

            Assert.AreEqual(0, allDiagnostics.Count);
        }
    }
}
