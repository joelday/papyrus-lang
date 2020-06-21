using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    [Flags]
    public enum UpdateProjectsOptions
    {
        None = 0,
        ReloadProjects = 1,
        ResolveExistingProjectSources = 2
    }

    public class ProjectManager : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly ILogger _logger;
        private readonly IProgramOptionsProvider _programOptionsProvider;
        private readonly Dictionary<string, ProjectHost> _projectHosts;
        private readonly ILanguageServer _languageServer;
        private readonly object _lock = new object();

        public ProjectManager(
            IServiceProvider serviceProvider,
            ILanguageServer languageServer,
            IProgramOptionsProvider programOptionsProvider,
            ILogger<ProjectManager> logger)
        {
            _serviceProvider = serviceProvider;
            _programOptionsProvider = programOptionsProvider;
            _languageServer = languageServer;
            _logger = logger;

            _projectHosts = new Dictionary<string, ProjectHost>();

            _logger.LogTrace("Project manager initialized");
        }

        public IEnumerable<ProjectHost> Projects
        {
            get
            {
                lock (_lock)
                {
                    return _projectHosts.Values.ToArray();
                }
            }
        }

        public ScriptFile GetScriptForFilePath(string filePath)
        {
            return _projectHosts.Values
                .Select(p => p.Program.ScriptByPaths.Lookup(filePath))
                .WhereLookup()
                .FirstOrDefault();
        }

        public Task PublishDiagnosticsForFilePath(string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    var script = Projects
                        .Select(p => p.Program.ScriptByPaths.Lookup(filePath))
                        .WhereLookup()
                        .FirstOrDefault();
                    script?.PublishDiagnostics(_languageServer.Document);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to publish diagnostics.");
                }
            });
        }

        // RefreshSources is for when files are added or removed.
        // ReloadProjects is for when a project or flags file has changed.
        public void UpdateProjects(UpdateProjectsOptions options = UpdateProjectsOptions.None)
        {
            lock (_lock)
            {
                _logger.LogTrace("Updating projects for workspaces");

                if ((options & UpdateProjectsOptions.ReloadProjects) != 0)
                {
                    _projectHosts.SynchronizeWithFactory(new HashSet<string>(), (key) => null);
                }

                var projects = _programOptionsProvider.GetProgramOptions().WaitForResult();

                _projectHosts.SynchronizeWithFactory(
                    new HashSet<string>(projects.Keys),
                    (key) =>
                    {
                        try
                        {
                            var projectHost = _serviceProvider.CreateInstance<ProjectHost>(projects[key]);
                            projectHost.ResolveSources();

                            return projectHost;
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Failed to load project: {0}", key);
                        }

                        return null;
                    },
                    updateOrReplaceExisting: (key, host) =>
                    {
                        if ((options & UpdateProjectsOptions.ResolveExistingProjectSources) != 0)
                        {
                            host.ResolveSources();
                        }

                        return host;
                    });
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _projectHosts.SynchronizeWithFactory(new HashSet<string>(), (key) => null);
            }
        }
    }
}