using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using DarkId.Papyrus.LanguageService.Projects;
using System.IO;

namespace DarkId.Papyrus.Test.LanguageService.Projects
{
    [TestClass]
    public class XmlProjectDeserializerTests
    {
        private readonly XmlProjectDeserializer _projectDeserializer = new XmlProjectDeserializer();

        [TestMethod]
        public void ShouldReturnADeserializedProject()
        {
            var project = _projectDeserializer.DeserializeProject(
                File.ReadAllText("../../../../scripts/Fallout 4/Fallout4.ppj"));

            // TODO: Non terrible test.
            Assert.AreEqual(0, project.Imports.Length);
        }

        [TestMethod]
        public void ShouldResolveVariableSubstitutions()
        {
            var project = _projectDeserializer.DeserializeProject(
                File.ReadAllText("../../../../scripts/MoD.ppj"));

            Assert.AreEqual(4, project.Imports.Length);
            Assert.AreEqual("F:\\repos\\mods\\skyrim\\Master of Disguise - Special Edition\\scripts\\Source", project.Imports[2]);
        }
    }
}
