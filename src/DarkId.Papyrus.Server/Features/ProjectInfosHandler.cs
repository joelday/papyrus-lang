using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Syntax;
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
        private readonly ILogger _logger;

        public ProjectInfosHandler(ProjectManager projectManager, ILogger<ProjectInfosHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public async Task<ProjectInfos> Handle(ProjectInfosParams request, CancellationToken cancellationToken)
        {
            if (_projectManager.Projects.Count == 0)
            {
                await _projectManager.UpdateProjects();
            }

            return new ProjectInfos()
            {
                Projects = new Container<ProjectInfo>(await _projectManager.Projects.Items
                    .Select(p =>
                    {
                        return Task.Run(async () =>
                        {
                            if (p.Sources == null)
                            {
                                await p.ResolveSources();
                            }

                            return new ProjectInfo()
                            {
                                Name = p.Name,
                                SourceIncludes = new Container<ProjectInfoSourceInclude>(p.Sources != null ? p.Sources.Select(include =>
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
                                }) : Enumerable.Empty<ProjectInfoSourceInclude>())
                            };
                        });
                    })
                    .ParallelAllAsync()
                    .ToArrayAsync())
            };
        }

        public void SetCapability(ProjectInfosCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
