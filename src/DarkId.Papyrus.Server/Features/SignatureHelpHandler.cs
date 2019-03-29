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
    [Method("textDocument/definition")]
    [Parallel]
    public class SignatureHelpHandler : ISignatureHelpHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public SignatureHelpHandler(ProjectManager projectManager, ILogger<SignatureHelpHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return Task.FromResult<SignatureHelp>(null);
                }

                var requestPosition = request.Position.ToPosition();

                var functionCallExpression = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<FunctionCallExpressionNode>(requestPosition);
                if (functionCallExpression == null)
                {
                    return Task.FromResult<SignatureHelp>(null);
                }

                var functionSymbol = functionCallExpression.Identifier.GetDeclaredOrReferencedSymbol();
                if (functionSymbol == null)
                {
                    return Task.FromResult<SignatureHelp>(null);
                }

                var activeParameterIndex = functionCallExpression.GetFunctionParameterIndexAtPosition(requestPosition);

                var displayTextEmitter = new DisplayTextEmitter();

                var displayText = functionSymbol is FunctionSymbol asFunctionSymbol ?
                    displayTextEmitter.GetDisplayTextForFunction(asFunctionSymbol) :
                    functionSymbol is EventSymbol asEventSymbol ?
                    displayTextEmitter.GetDisplayTextForEvent(asEventSymbol) : null;

                if (displayText == null)
                {
                    return Task.FromResult<SignatureHelp>(null);
                }

                return Task.FromResult(new SignatureHelp()
                {
                    ActiveParameter = activeParameterIndex,
                    Signatures = new SignatureInformation[]
                    {
                        new SignatureInformation()
                        {
                            Documentation = displayText.Documentation,
                            Label = $"{displayText.ShortNamePrefix}({displayText.Parameters.Select(p => p.Text).Join(", ")})",
                            Parameters = displayText.Parameters.Select(p => new ParameterInformation()
                            {
                                Label = p.Text
                            }).ToArray()
                        }
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<SignatureHelp>(null);
        }

        public void SetCapability(SignatureHelpCapability capability)
        {
            capability.DynamicRegistration = true;
        }

        public SignatureHelpRegistrationOptions GetRegistrationOptions()
        {
            return new SignatureHelpRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector,
                TriggerCharacters = new string[] { "(", "," }
            };
        }
    }
}
