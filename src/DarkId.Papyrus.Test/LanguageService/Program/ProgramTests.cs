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
    public class ProgramTests : ProgramTestBase
    {
        [TestMethod]
        public async Task Program_ShouldResolveSources()
        {
            await Program.ResolveSources();

#if FALLOUT4
            var sourcesPath = "../../../../scripts/Fallout 4";
#elif SKYRIM
            var sourcesPath = "../../../../scripts/Skyrim";
#endif

            Console.WriteLine(Program.FilePaths.Keys.Join(",\r\n"));

            // TODO: This won't work once any directories are added inside Base.
            CollectionAssert.AreEquivalent(
                Program.FilePaths.Keys.ToList(),
                Directory.EnumerateFiles(Path.GetFullPath(sourcesPath), "*.psc", SearchOption.AllDirectories).ToList());
        }
    }
}
