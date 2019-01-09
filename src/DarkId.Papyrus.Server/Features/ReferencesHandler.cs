using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/references")]
    [Parallel]
    public class ReferencesHandler : IReferencesHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public ReferencesHandler(ProjectManager projectManager, ILogger<CompletionHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return null;
                }

                var identifier = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<IdentifierNode>(request.Position.ToPosition());
                if (identifier == null)
                {
                    return null;
                }

                var symbol = identifier.GetDeclaredOrReferencedSymbol();
                if (symbol == null)
                {
                    return null;
                }

                var referencingNodes = await symbol.FindReferences(cancellationToken);

                return referencingNodes.Select(node => new Location()
                {
                    Range = node.Range.ToRange(),
                    Uri = PathUtilities.ToFileUri(node.GetScriptFile().FilePath)
                }).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while handling request.");
            }

            return null;
        }

        public void SetCapability(ReferencesCapability capability)
        {
            capability.DynamicRegistration = true;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }
    }
}
