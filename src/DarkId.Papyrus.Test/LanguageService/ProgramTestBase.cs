using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using NUnit.Framework;
using System.Collections.Generic;

namespace DarkId.Papyrus.Test.LanguageService
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LanguageVersionsAttribute : Attribute
    {
        public LanguageVersion[] Versions { get; }

        public LanguageVersionsAttribute(LanguageVersion version, params LanguageVersion[] versions)
        {
            Versions = new List<LanguageVersion>().Append(version).Concat(versions).ToArray();
        }
    }

    [TestFixture]
    [TestFixtureSource(typeof(PerLanguageFixtureData))]
    public abstract class PerLanguageProgramTestBase : IDisposable
    {
        protected PapyrusProgram Program { get; private set; }
        protected LanguageVersion LanguageVersion { get; private set; }

        [OneTimeSetUp]
        public void CreateProgram()
        {
            LanguageVersion = (LanguageVersion)TestContext.CurrentContext.Test.Properties["LanguageVersion"].First();
            Program = new TestServiceInstance(LanguageVersion).CreateProgram();
            Program.ResolveSources().Wait();
        }

        [OneTimeTearDown]
        public void DisposeProgram()
        {
            Program.Dispose();
        }

        [SetUp]
        public void ValidateVersionIsApplicable()
        {
            var testMethod = GetType().GetMethod(TestContext.CurrentContext.Test.MethodName);
            var attribute = testMethod?.GetCustomAttribute<LanguageVersionsAttribute>();

            if (attribute != null && !attribute.Versions.Contains(LanguageVersion))
            {
                Assert.Ignore($"This test does not apply to {LanguageVersion}");
            }
        }

        public void Dispose()
        {
            Program.Dispose();
        }
    }
}
