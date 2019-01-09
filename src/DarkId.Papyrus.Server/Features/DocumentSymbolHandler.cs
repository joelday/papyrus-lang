using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/documentSymbol")]
    [Parallel]
    public class DocumentSymbolHandler : IDocumentSymbolHandler
    {
        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public static SymbolKind GetSymbolKind(PapyrusSymbol symbol)
        {
            switch (symbol.Kind)
            {
                case SymbolKinds.CustomEvent:
                case SymbolKinds.Event:
                    return SymbolKind.Event;
                case SymbolKinds.Function:
                    return SymbolKind.Method;
                case SymbolKinds.State:
                case SymbolKinds.Group:
                    return SymbolKind.Namespace;
                case SymbolKinds.Variable:
                    if (symbol.Parent.Kind == SymbolKinds.Script)
                    {
                        return SymbolKind.Field;
                    }

                    return SymbolKind.Variable;
                case SymbolKinds.Property:
                    return SymbolKind.Property;
                case SymbolKinds.Struct:
                    return SymbolKind.Struct;
                case SymbolKinds.Import:
                case SymbolKinds.Script:
                    return SymbolKind.Class;
                default:
                    return SymbolKind.Object;
            }
        }

        private static void ToDocumentSymbol(Uri fileUri, IReadOnlyScriptText text, PapyrusSymbol symbol, List<DocumentSymbolInformation> symbolInformationContainer, string containerName = null)
        {
            var symbolInformation = new DocumentSymbolInformation()
            {
                Name = symbol.Name,
                Kind = GetSymbolKind(symbol),
                Location = new Location()
                {
                    Uri = fileUri,
                    Range = symbol.Definition.Range.ToRange()
                },
                ContainerName = containerName
            };

            foreach (var childSymbol in symbol.Children)
            {
                ToDocumentSymbol(fileUri, text, childSymbol, symbolInformationContainer, symbolInformation.Name);
            }

            symbolInformationContainer.Add(symbolInformation);
        }

        public DocumentSymbolHandler(ProjectManager projectManager, ILogger<DocumentSymbolHandler> logger)
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

        public Task<DocumentSymbolInformationContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                var scriptSymbol = scriptFile?.Symbol;
                if (scriptSymbol == null)
                {
                    return Task.FromResult<DocumentSymbolInformationContainer>(null);
                }

                var symbolInformationContainer = new List<DocumentSymbolInformation>();
                ToDocumentSymbol(request.TextDocument.Uri, scriptFile.Text, scriptSymbol, symbolInformationContainer);

                return Task.FromResult(new DocumentSymbolInformationContainer(symbolInformationContainer));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while handling request.");
            }

            return Task.FromResult<DocumentSymbolInformationContainer>(null);
        }

        public void SetCapability(DocumentSymbolCapability capability)
        {
            capability.DynamicRegistration = true;
            capability.HierarchicalDocumentSymbolSupport = true;
        }
    }
}
