using Antlr.Runtime;
using Antlr.Runtime.Tree;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.External;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.Server.Protocol;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Features
{
    [Method("papyrus/projectInfos")]
    [Parallel]
    public class ProjectInfosHandler : IProjectInfosHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public ProjectInfosHandler(ProjectManager projectManager, IFileSystem fileSystem, ILogger<ProjectInfosHandler> logger)
        {
            _projectManager = projectManager;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public Task<ProjectInfos> Handle(ProjectInfosParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProjectInfos()
            {
                Projects = new Container<ProjectInfo>(_projectManager.Projects.Select(p =>
                {
                    return new ProjectInfo()
                    {
                        Name = p.Name,
                        SourceIncludes = new Container<ProjectInfoSourceInclude>(_fileSystem.ResolveSourceFileIncludes(p.Program.Options.Sources).WaitForResult().Select(include =>
                        {
                            var name = !string.IsNullOrEmpty(include.Key.Path) ? Path.GetFileName(include.Key.Path) : "Scripts";

                            return new ProjectInfoSourceInclude()
                            {
                                Name = name,
                                FullPath = include.Key.Path,
                                IsImport = include.Key.IsImport,
                                Scripts = new Container<ProjectInfoScript>(include.Value.Select(script => new ProjectInfoScript()
                                {
                                    Identifier = script.Key,
                                    FilePath = script.Value
                                }))
                            };
                        }))
                    };
                }))
            }); 
        }

        public void SetCapability(ProjectInfosCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
