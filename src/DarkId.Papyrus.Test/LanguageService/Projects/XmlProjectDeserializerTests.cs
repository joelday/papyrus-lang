using System;
using DarkId.Papyrus.LanguageService.Compiler;
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
                File.ReadAllText("../../../../../papyrus/FO4TestScripts/Project/Project.ppj"));

            Assert.AreEqual(project.Imports.Length, 2);
            Assert.AreEqual(project.Imports[1], "..\\..\\FO4Scripts\\Base");
        }
    }
}
