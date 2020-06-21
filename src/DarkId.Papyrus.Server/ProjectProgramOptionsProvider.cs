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
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    public class ProjectProgramOptionsProvider : IProgramOptionsProvider
    {
        private readonly string _projectFlagsFileName;
        private readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;
        private readonly IXmlProjectLocator _projectLocator;
        private readonly IXmlProjectLoader _projectLoader;
        private readonly CreationKitProgramOptionsProvider _ambientOptionsProvider;
        private readonly ILogger _logger;

        public ProjectProgramOptionsProvider(
            string projectFlagsFileName,
            OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer,
            IXmlProjectLocator projectLocator,
            IXmlProjectLoader projectLoader,
            CreationKitProgramOptionsProvider ambientOptionsProvider,
            ILogger<ProjectProgramOptionsProvider> logger)
        {
            _projectFlagsFileName = projectFlagsFileName;
            _languageServer = languageServer;
            _projectLocator = projectLocator;
            _projectLoader = projectLoader;
            _ambientOptionsProvider = ambientOptionsProvider;

            _logger = logger;
        }

        public async Task<Dictionary<string, ProgramOptions>> GetProgramOptions()
        {
            var workspaceFolders = await Task.Run(() => _languageServer.Workspace.WorkspaceFolders());
            var workspaceFolderPaths = workspaceFolders?.Select(f => f.Uri.ToFilePath()).ToArray() ?? new string[] { };

            var options = await workspaceFolderPaths.ToAsyncEnumerable()
                .Select(d =>
                {
                    return (Path: d, Files: _projectLocator.FindProjectFiles(d));
                })
                .Distinct(p => p.Path)
                .SelectMany(s => s.Files)
                .Distinct()
                .SelectAwait(async projectPath =>
                {
                    try
                    {
                        var project = await _projectLoader.LoadProject(projectPath);
                        return (ProjectPath: projectPath, Options: new ProgramOptionsBuilder().WithProject(project).Build());
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Failed to load project file.");
                    }

                    return (ProjectPath: projectPath, Options: null);
                })
                .Where(p => p.Options?.FlagsFileName.CaseInsensitiveEquals(_projectFlagsFileName) ?? false)
                .ToDictionaryAsync(p => p.ProjectPath, p => p.Options);

            var ambientOptions = _ambientOptionsProvider.GetAmbientProgramOptions();
            options.Add(ambientOptions.Name, ambientOptions);

            return options;
        }
    }
}