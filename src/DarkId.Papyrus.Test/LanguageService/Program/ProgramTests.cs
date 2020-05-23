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
    public class ProgramTests : PerLanguageProgramTestBase
    {
        [Test]
        public async Task Program_ShouldResolveSources()
        {
            var scriptsDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../scripts");

            await Program.ResolveSources();

            var sourcesPath = Path.Combine(scriptsDirectory,
                LanguageVersion == LanguageVersion.Fallout4 ? "Fallout 4" : "Skyrim");

            var sharedSources = Path.Combine(scriptsDirectory, "Shared");

            var sources = EnumerableExtensions.Of(sourcesPath, sharedSources)
                .SelectMany(path => Directory.EnumerateFiles(Path.GetFullPath(path), "*.psc", SearchOption.AllDirectories))
                .DistinctBy(path => Path.GetFileName(path))
                .ToList();

            CollectionAssert.AreEquivalent(
                Program.FilePaths.Keys.ToList(),
                sources);
        }
    }
}
