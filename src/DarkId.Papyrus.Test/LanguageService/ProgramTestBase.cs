using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LanguageVersionsAttribute : Attribute
    {
        public LanguageVersion[] Versions { get; }

        public LanguageVersionsAttribute(params LanguageVersion[] versions)
        {
            Versions = versions;
        }
    }

    [TestFixture]
    [TestFixtureSource(typeof(PerLanguageFixtureData))]
    public abstract class ProgramTestBase : IDisposable
    {
        protected PapyrusProgram Program { get; }
        protected LanguageVersion LanguageVersion => Program.Options.LanguageVersion;

        protected ProgramTestBase(PapyrusProgram program)
        {
            Program = program;
            Program.ResolveSources().Wait();
        }

        [SetUp]
        public void PreLanguageSetup()
        {
            var testMethod = GetType().GetMethod(TestContext.CurrentContext.Test.MethodName);
            var attribute = testMethod?.GetCustomAttribute<LanguageVersionsAttribute>();

            if (attribute != null && !attribute.Versions.Contains(Program.Options.LanguageVersion))
            {
                Assert.Ignore($"This test only applies to {string.Join(", ", attribute.Versions)}");
            }
        }

        public void Dispose()
        {
            Program.Dispose();
        }
    }
}
