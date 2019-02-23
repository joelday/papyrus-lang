using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitProgramOptionsProvider
    {
        private readonly string _ambientProgramName;
        private readonly string _flagsFileName;
        private readonly CreationKitConfig _defaultConfig;
        private readonly ICreationKitInisLocator _inisLocator;
        private readonly ICreationKitConfigLoader _configLoader;
        private readonly ILogger _logger;

        public CreationKitProgramOptionsProvider(
            string ambientProgramName,
            string flagsFileName,
            CreationKitConfig defaultConfig,
            ICreationKitInisLocator inisLocator,
            ICreationKitConfigLoader configLoader,
            ILogger<CreationKitProgramOptionsProvider> logger)
        {
            _ambientProgramName = ambientProgramName;
            _flagsFileName = flagsFileName;
            _defaultConfig = defaultConfig;
            _inisLocator = inisLocator;
            _configLoader = configLoader;
            _logger = logger;
        }

        public ProgramOptions GetAmbientProgramOptions()
        {
            var iniLocations = _inisLocator.GetIniLocations();
            var config = _configLoader.LoadConfig(iniLocations);
            
            if (config == null)
            {
                return null;
            }

            var installPath = config.CreationKitInstallPath;
            var scriptSourceFolder = config.Config.Papyrus?.sScriptSourceFolder ?? _defaultConfig.Papyrus.sScriptSourceFolder;
            var additionalImports = config.Config.Papyrus?.sAdditionalImports ?? _defaultConfig.Papyrus.sAdditionalImports;

            var sourceDirectoryPath = string.IsNullOrEmpty(scriptSourceFolder) ?
                null :
                PathUtilities.GetCombinedOrRooted(installPath, scriptSourceFolder.Replace("\"", ""));

            var importPathsElementsWithSubstitutedSource = string.IsNullOrEmpty(additionalImports) ?
                new List<string>() :
                additionalImports.Replace("\"", "").Split(';')
                .Select(importPath => importPath.CaseInsensitiveEquals("$(source)") ? sourceDirectoryPath : importPath)
                .ToList();

            if (!string.IsNullOrEmpty(sourceDirectoryPath) && !importPathsElementsWithSubstitutedSource.Contains(sourceDirectoryPath))
            {
                importPathsElementsWithSubstitutedSource.Add(sourceDirectoryPath);
            }

            var programOptions = new ProgramOptionsBuilder()
                .WithName(_ambientProgramName)
                .WithFlagsFileName(_flagsFileName)
                .WithSourceIncludes(importPathsElementsWithSubstitutedSource
                .Select(path => PathUtilities.GetCombinedOrRooted(installPath, path))
                .Select(path => new SourceInclude()
                {
                    Path = path
                }).Reverse())
                .Build();

            return programOptions;
        }
    }
}