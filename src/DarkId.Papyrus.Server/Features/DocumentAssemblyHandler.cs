using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.Server.Protocol;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/assembly")]
    [Parallel]
    public class DocumentAssemblyHandler : IDocumentAssemblyHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public DocumentAssemblyHandler(ProjectManager projectManager, ILogger<DocumentAssemblyHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public Task<DocumentAssembly> Handle(DocumentAssemblyParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return Task.FromResult<DocumentAssembly>(null);
                }

                return Task.FromResult(new DocumentAssembly()
                {
                    Assembly = scriptFile.GetScriptAssembly()
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<DocumentAssembly>(null);
        }

        public void SetCapability(DocumentAssemblyCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
