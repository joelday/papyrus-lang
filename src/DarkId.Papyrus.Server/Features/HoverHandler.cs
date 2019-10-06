using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/hover")]
    [Parallel]
    public class HoverHandler : IHoverHandler
    {
        public static MarkupContent DisplayTextToMarkupContent(DisplayText displayText)
        {
            var sb = new StringBuilder();
            sb.AppendLine("```papyrus");
            sb.AppendLine(displayText.Text);
            sb.AppendLine("```");

            if (!string.IsNullOrEmpty(displayText.Documentation))
            {
                sb.AppendLine("___");
                sb.AppendLine(displayText.Documentation);
            }

            return new MarkupContent()
            {
                Kind = MarkupKind.Markdown,
                Value = sb.ToString()
            };
        }

        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public HoverHandler(ProjectManager projectManager, ILogger<HoverHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return Task.FromResult<Hover>(null);
                }

                var identifier = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<IdentifierNode>(request.Position.ToPosition());
                if (identifier == null)
                {
                    return Task.FromResult<Hover>(null);
                }

                var symbol = identifier.GetDeclaredOrReferencedSymbol();
                if (symbol == null)
                {
                    return Task.FromResult<Hover>(null);
                }

                var displayText = new DisplayTextEmitter().GetDisplayText(symbol);

                return Task.FromResult(new Hover()
                {
                    Contents = new MarkedStringsOrMarkupContent(DisplayTextToMarkupContent(displayText)),
                    Range = identifier.Range.ToRange()
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<Hover>(null);
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public void SetCapability(HoverCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
