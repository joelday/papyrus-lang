using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Projects;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server
{
    public class ProjectManager : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly ILogger _logger;
        private readonly IProgramOptionsProvider _programOptionsProvider;
        private readonly SourceCache<ProjectOptionsObject, StringOrdinalIgnore> _projectHostDriver = new SourceCache<ProjectOptionsObject, StringOrdinalIgnore>(o => o.Filepath);
        public IObservableCache<ProjectHost, StringOrdinalIgnore> Projects { get; }
        private readonly ILanguageServer _languageServer;
        private readonly AsyncLock _projectUpdateLock = new AsyncLock();

        class ProjectOptionsObject
        {
            public StringOrdinalIgnore Filepath;
            public ProgramOptions Options;
        }

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

            _logger.LogTrace("Project manager initialized");

            Projects = _projectHostDriver.Connect()
                .TransformAsync(
                    async o =>
                    {
                        try
                        {
                            var projectHost = _serviceProvider.CreateInstance<ProjectHost>(o.Options);
                            await projectHost.ResolveSources();

                            return projectHost;
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Failed to load project: {0}", o.Filepath);
                        }

                        return default(ProjectHost);
                    },
                    // ToDo
                    // Jack up?
                    maximumConcurrency: 1)
                .AsObservableCache();
        }

        public ScriptFile GetScriptForFilePath(string filePath)
        {
            return Projects.Items
                .Select(p => p.Program.ScriptByPaths.Lookup(filePath))
                .WhereLookup()
                .FirstOrDefault();
        }

        public void PublishDiagnosticsForFilePath(string filePath)
        {
            var script = Projects.Items
                .Select(p => p.Program.ScriptByPaths.Lookup(filePath))
                .WhereLookup()
                .FirstOrDefault();
            script?.PublishDiagnostics(_languageServer.Document);
        }

        public async Task UpdateProjects()
        {
            // Lock, just to ensure only one user is updating
            using var lockUsage = await _projectUpdateLock.WaitAsync();

            _logger.LogTrace("Updating projects for workspaces");

            var projects = await _programOptionsProvider.GetProgramOptions();
            _projectHostDriver.SetTo(
                keySelector: o => o.Filepath,
                projects.Select(kv => new ProjectOptionsObject()
                {
                    Filepath = kv.Key,
                    Options = kv.Value,
                }),
                setTo: CacheExtensions.SetToEnum.SetExisting);
        }

        public Task ResolveExistingProjectSources()
        {
            return Task.WhenAll(Projects.Items.Select(i => i.ResolveSources()));
        }

        public void Dispose()
        {
            _projectHostDriver.Clear();
        }
    }
}