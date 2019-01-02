using System;
using DarkId.Papyrus.LanguageService.Compiler;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using DarkId.Papyrus.LanguageService.Projects;
using System.IO;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;

namespace DarkId.Papyrus.Test.LanguageService.Configuration.CreationKit
{
    [TestClass]
    public class CreationKitInisConfigLoaderTests
    {
        private readonly CreationKitInisConfigLoader _configLoader = new CreationKitInisConfigLoader();

        [TestMethod]
        public void ShouldLoadAConfigFromIniFiles()
        {
            var config = _configLoader.LoadConfig(new CreationKitIniLocations()
            {
                CreationKitInstallPath = "../../../../../papyrus/",
                RelativeIniPaths = new List<string>() {
                    "CreationKit1.ini",
                    "CreationKit2.ini"
                }
            });

            Assert.AreEqual(config.CreationKitInstallPath, "../../../../../papyrus/");

            Assert.AreEqual(config.Config.Papyrus.sScriptSourceFolder, "FO4TestScripts\\Project");
            Assert.AreEqual(config.Config.Papyrus.sAdditionalImports, "FO4Scripts\\Base");
        }
    }
}
