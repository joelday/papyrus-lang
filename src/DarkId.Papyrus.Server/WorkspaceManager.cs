using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Microsoft.Extensions.DependencyInjection;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.External;

namespace DarkId.Papyrus.Server
{
    public class WorkspaceManager : IDidChangeWorkspaceFoldersHandler, IDidSaveTextDocumentHandler, IDidChangeWatchedFilesHandler
    {
        private readonly OmniSharp.Extensions.LanguageServer.Server.ILanguageServer _languageServer;
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly ProjectManager _projectManager;

        public WorkspaceManager(
            OmniSharp.Extensions.LanguageServer.Server.ILanguageServer languageServer,
            ILogger<WorkspaceManager> logger,
            IServiceProvider serviceProvider,
            ProjectManager projectManager)
        {
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                logger.LogError(e.Exception, "Unobserved exception thrown.");
            };

            _languageServer = languageServer;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _projectManager = projectManager;

            var textProvider = (TextDocumentScriptTextProvider)_serviceProvider.GetService<IScriptTextProvider>();

            textProvider.OnDidOpenTextDocument += async (s, e) =>
            {
                var filePath = e.TextDocument.Uri.ToFilePath();

                if (Path.GetExtension(filePath).CaseInsensitiveEquals(".psc") && _projectManager.Projects.Count() == 0)
                {
                    await UpdateProjects(UpdateProjectsOptions.ReloadProjects);
                }

                await _projectManager.PublishDiagnosticsForFilePath(filePath);
            };

            _languageServer.AddHandlers(textProvider);
        }

        private Task UpdateProjects(UpdateProjectsOptions options = UpdateProjectsOptions.None)
        {
            _projectManager.UpdateProjects(options);
            return Task.FromResult<object>(null);
        }

        public async Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken)
        {
            await UpdateProjects(UpdateProjectsOptions.ReloadProjects);
            return Unit.Value;
        }

        public void SetCapability(DidChangeWorkspaceFolderCapability capability)
        {
            capability.DynamicRegistration = true;
        }

        public async Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            if (Path.GetExtension(request.TextDocument.Uri.ToFilePath()).CaseInsensitiveEquals(".ppj"))
            {
                await UpdateProjects(UpdateProjectsOptions.ReloadProjects);
            }

            return Unit.Value;
        }

        public TextDocumentSaveRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusProjectsAndScriptsSelector,
                IncludeText = false,
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            capability.DidSave = true;
            capability.DynamicRegistration = true;
        }

        public async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
        {
            var createdOrDeleted = request.Changes.Where(c => c.Type == FileChangeType.Created || c.Type == FileChangeType.Deleted);

            if (createdOrDeleted.Any(c => Path.GetExtension(c.Uri.ToFilePath()).CaseInsensitiveEquals(".psc")))
            {
                await UpdateProjects(UpdateProjectsOptions.ReloadProjects);
            }
            else if (createdOrDeleted.Any(c => Path.GetExtension(c.Uri.ToFilePath()).CaseInsensitiveEquals(".psc")))
            {
                await UpdateProjects(UpdateProjectsOptions.ResolveExistingProjectSources);
            }

            return Unit.Value;
        }

        object IRegistration<object>.GetRegistrationOptions()
        {
            return new object();
        }

        public void SetCapability(DidChangeWatchedFilesCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}