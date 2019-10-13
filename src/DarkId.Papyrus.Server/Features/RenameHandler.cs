using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Syntax.Legacy;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/rename")]
    [Parallel]
    public class RenameHandler : IRenameHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public RenameHandler(ProjectManager projectManager, ILogger<RenameHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
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
                var referencingNodeEdits = referencingNodes.Select(node =>
                    new KeyValuePair<Uri, TextEdit>(
                        PathUtilities.ToFileUri(node.GetScriptFile().FilePath),
                        new TextEdit() { NewText = request.NewName, Range = node.Range.ToRange() }));

                var referencingNodeEditDictionary = referencingNodeEdits.GroupBy(k => k.Key).ToDictionary(k => k.Key, k => k.Select(v => v.Value));

                return new WorkspaceEdit()
                {
                    Changes = referencingNodeEditDictionary
                };
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return null;
        }

        RenameRegistrationOptions IRegistration<RenameRegistrationOptions>.GetRegistrationOptions()
        {
            return new RenameRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public void SetCapability(RenameCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
