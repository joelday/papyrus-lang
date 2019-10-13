using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    public class PerLanguageProgramTests : PerLanguageProgramTestBase
    {
        public PerLanguageProgramTests(PapyrusProgram program) : base(program)
        {
        }

        [Test]
        public async Task Program_ShouldResolveSources()
        {
            await Program.ResolveSources();
            var sourcesPath = Program.Options.LanguageVersion ==
                              LanguageVersion.Fallout4 ? "../../../scripts/Fallout 4" : "../../../scripts/Skyrim";

            Console.WriteLine(Program.FilePaths.Keys.Join(",\r\n"));

            // TODO: This won't work once any directories are added inside Base.
            CollectionAssert.AreEquivalent(
                Program.FilePaths.Keys.ToList(),
                Directory.EnumerateFiles(Path.GetFullPath(sourcesPath), "*.psc", SearchOption.AllDirectories).ToList());
        }
    }
}
