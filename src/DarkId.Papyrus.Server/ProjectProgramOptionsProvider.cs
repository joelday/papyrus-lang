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
        private readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;
        private readonly IXmlProjectLocator _projectLocator;
        private readonly IXmlProjectLoader _projectLoader;
        private readonly CreationKitProgramOptionsProvider _ambientOptionsProvider;
        private readonly ILogger _logger;

        public ProjectProgramOptionsProvider(
            OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer,
            IXmlProjectLocator projectLocator,
            IXmlProjectLoader projectLoader,
            CreationKitProgramOptionsProvider ambientOptionsProvider,
            ILogger<ProjectProgramOptionsProvider> logger)
        {
            _languageServer = languageServer;
            _projectLocator = projectLocator;
            _projectLoader = projectLoader;
            _ambientOptionsProvider = ambientOptionsProvider;

            _logger = logger;
        }

        public async Task<Dictionary<string, ProgramOptions>> GetProgramOptions()
        {
            var workspaceFolders = await Task.Run(() => _languageServer.Workspace.WorkspaceFolders().WaitForResult());
            var workspaceFolderPaths = workspaceFolders.Select(f => f.Uri.ToFilePath()).ToArray();

            var workspaceProjectFiles = Task.WhenAll(workspaceFolderPaths.Select(async d =>
            {
                var files = await _projectLocator.FindProjectFiles(d);
                return new Tuple<string, IEnumerable<string>>(d, files);
            }))
            .WaitForResult()
            .ToDictionary(t => t.Item1, t => t.Item2);

            var options = workspaceProjectFiles
                .SelectMany(s => s.Value)
                .Distinct()
                .Select(projectPath =>
                {
                    try
                    {
                        var project = _projectLoader.LoadProject(projectPath).WaitForResult();
                        return new Tuple<string, ProgramOptions>(projectPath, new ProgramOptionsBuilder().WithProject(project).Build());
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to load project file.");
                    }

                    return new Tuple<string, ProgramOptions>(projectPath, null);
                })
                .Where(p => p.Item2 != null)
                .ToDictionary(p => p.Item1, p => p.Item2);

            var workspacesWithoutProjects = workspaceProjectFiles.Where(p => p.Value.Count() == 0).Select(p => p.Key);
            if (workspacesWithoutProjects.Any())
            {
                var ambientOptions = _ambientOptionsProvider.GetAmbientProgramOptions();
                foreach (var ambientWorkspacePath in workspacesWithoutProjects)
                {
                    var workspaceAmbientOptions = ambientOptions.Clone();
                    workspaceAmbientOptions.Name = Path.GetFileName(ambientWorkspacePath);
                    workspaceAmbientOptions.Sources.Includes.Insert(0, new SourceInclude()
                    {
                        Path = ambientWorkspacePath
                    });

                    options.Add(ambientWorkspacePath, workspaceAmbientOptions);
                }
            }

            return options;
        }
    }
}