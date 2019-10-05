using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/definition")]
    [Parallel]
    public class DefinitionHandler : IDefinitionHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public DefinitionHandler(ProjectManager projectManager, ILogger<DefinitionHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return Task.FromResult<LocationOrLocationLinks>(null);
                }

                var identifier = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<IdentifierNode>(request.Position.ToPosition());
                if (identifier == null)
                {
                    return Task.FromResult<LocationOrLocationLinks>(null);
                }

                var symbol = identifier.GetDeclaredOrReferencedSymbol();
                if (symbol == null)
                {
                    return Task.FromResult<LocationOrLocationLinks>(null);
                }

                var definitionFile = symbol.Script.Definition.GetScriptFile();
                if (definitionFile == null)
                {
                    return Task.FromResult<LocationOrLocationLinks>(null);
                }

                return Task.FromResult(new LocationOrLocationLinks(new Location()
                {
                    Uri = PathUtilities.ToFileUri(symbol.Script.Definition.GetScriptFile().FilePath),
                    Range = symbol.Identifier.Range.ToRange()
                }));
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<LocationOrLocationLinks>(null);
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public void SetCapability(DefinitionCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
