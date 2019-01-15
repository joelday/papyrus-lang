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
        private readonly ICreationKitInisLocator _inisLocator;
        private readonly ICreationKitConfigLoader _configLoader;
        private readonly ILogger _logger;

        public CreationKitProgramOptionsProvider(
            ICreationKitInisLocator inisLocator,
            ICreationKitConfigLoader configLoader,
            ILogger<CreationKitProgramOptionsProvider> logger)
        {
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
            var scriptSourceFolder = config.Config.Papyrus?.sScriptSourceFolder;
            var additionalImports = config.Config.Papyrus?.sAdditionalImports;

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
                .WithName("CreationKit")
                // TODO: Make these configurable?
#if FALLOUT4
                .WithFlagsFileName("Institute_Papyrus_Flags.flg") 
#elif SKYRIM
                .WithFlagsFileName("TESV_Papyrus_Flags.flg")
#endif
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