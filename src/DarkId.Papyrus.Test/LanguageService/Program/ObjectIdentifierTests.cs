using System;
using System.IO;
using System.Text.RegularExpressions;
using DarkId.Papyrus.LanguageService.Program;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    [TestClass]
    public class ObjectIdentifierTests
    {
        [TestMethod]
        public void ShouldParseANonNamespacedScriptName()
        {
            var identifier = ObjectIdentifier.Parse("ScriptName");
            Assert.AreEqual(identifier.ScriptName, "ScriptName");
        }

        [TestMethod]
        public void ShouldParseANamespacedScriptName()
        {
            var identifier = ObjectIdentifier.Parse("NamespaceA:NamespaceB:ScriptName");
            Assert.AreEqual(identifier.NamespaceParts.Length, 2);
            Assert.AreEqual(identifier.NamespaceParts[0], "NamespaceA");
            Assert.AreEqual(identifier.NamespaceParts[1], "NamespaceB");
            Assert.AreEqual(identifier.ScriptName, "ScriptName");
        }

        [TestMethod]
        public void ShouldParseANamespacedScriptWithMemberName()
        {
            var identifier = ObjectIdentifier.Parse("NamespaceA:NamespaceB:ScriptName#StructName");
            Assert.AreEqual(identifier.StructName, "StructName");
        }

        [TestMethod]
        public void ShouldBeEquatableWithCaseInsensitivity()
        {
            var identifierA = ObjectIdentifier.Parse("NamespaceA:NamespaceB:ScriptName#StructName");
            var identifierB = ObjectIdentifier.Parse("namespacea:namespaceb:scriptname#structname");

            Assert.IsTrue(identifierA == identifierB);
            Assert.AreEqual(identifierA, identifierB);

            var identifierC = ObjectIdentifier.Parse("NamespaceA:NamespaceB:ScriptName");

            Assert.IsTrue(identifierB != identifierC);
            Assert.AreNotEqual(identifierB, identifierC);
        }

        [TestMethod]
        public void ShouldParseAndConvertToAFilePath()
        {
            var identifierA = ObjectIdentifier.Parse("NamespaceA:NamespaceB:ScriptName");
            var identifierB = ObjectIdentifier.FromScriptFilePath("NamespaceA/NamespaceB/ScriptName.psc");
            var path = identifierB.ToScriptFilePath();
            var identifierC = ObjectIdentifier.FromScriptFilePath(path);

            Assert.AreEqual(identifierA, identifierB);
            Assert.AreEqual(identifierB, identifierC);
        }
    }
}