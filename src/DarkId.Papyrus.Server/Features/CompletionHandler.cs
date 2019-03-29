﻿using System;
using System.Linq;
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
    [Method("textDocument/completion")]
    [Parallel]
    public class CompletionHandler : ICompletionHandler
    {
        public static CompletionItemKind GetCompletionItemKind(PapyrusSymbol symbol)
        {
            switch (symbol.Kind)
            {
                case SymbolKinds.CustomEvent:
                case SymbolKinds.Event:
                    return CompletionItemKind.Event;
                case SymbolKinds.Function:
                    return CompletionItemKind.Method;
                case SymbolKinds.Variable:
                    if (symbol.Parent == null ||
                        symbol.Parent.Kind == SymbolKinds.Script ||
                        symbol.Parent.Kind == SymbolKinds.Struct)
                    {
                        return CompletionItemKind.Field;
                    }

                    return CompletionItemKind.Variable;
                case SymbolKinds.Property:
                    return CompletionItemKind.Property;
                case SymbolKinds.Struct:
                    return CompletionItemKind.Struct;
                case SymbolKinds.Import:
                case SymbolKinds.Script:
                    return CompletionItemKind.Class;
                default:
                    return CompletionItemKind.Text;
            }
        }

        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;

        public CompletionHandler(ProjectManager projectManager, ILogger<CompletionHandler> logger)
        {
            _projectManager = projectManager;
            _logger = logger;
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector,
                TriggerCharacters = new string[] { "." }
            };
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            try
            {
                var scriptFile = _projectManager.GetScriptForFilePath(request.TextDocument.Uri.ToFilePath());
                if (scriptFile == null)
                {
                    return Task.FromResult<CompletionList>(null);
                }

                var node = scriptFile.Node.GetDescendantNodeAtPosition(request.Position.ToPosition()) ?? scriptFile.Node;
                if (node == null)
                {
                    return Task.FromResult<CompletionList>(null);
                }

                var displayTextEmitter = new DisplayTextEmitter();

                var callExpressionNode = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<FunctionCallExpressionNode>(request.Position.ToPosition());
                var callExpressionParameterIndex = callExpressionNode?.GetFunctionParameterIndexAtPosition(request.Position.ToPosition());
                var valuesAreExclusive = false;

                var knownParamValues = callExpressionParameterIndex.HasValue && callExpressionParameterIndex != -1 ?
                    callExpressionNode.GetKnownParameterValueSymbols(callExpressionParameterIndex.Value, out valuesAreExclusive) : null;

                var knownParamValuesCompletions = knownParamValues != null ?
                    knownParamValues.Select(symbol => {
                        var displayText = displayTextEmitter.GetDisplayText(symbol);

                        return new CompletionItem()
                        {
                            Kind = GetCompletionItemKind(symbol), 
                            Label = symbol.Name,
                            InsertText = $"\"{symbol.Name}\"",
                            Detail = displayText.Text,
                            SortText = $"_{symbol.Name}",
                            Documentation = displayText.Documentation
                        };
                    }).ToArray() : Enumerable.Empty<CompletionItem>();

                if (valuesAreExclusive)
                {
                    return Task.FromResult(new CompletionList(knownParamValuesCompletions));
                }

                var symbols = node.GetReferencableSymbols();
                var symbolCompletions = symbols.Select(symbol => {
                    var displayText = displayTextEmitter.GetDisplayText(symbol);

                    return new CompletionItem()
                    {
                        Kind = GetCompletionItemKind(symbol),
                        Label = symbol.Name,
                        Detail = displayText.Text,
                        SortText = symbol.Name,
                        Documentation = displayText.Documentation
                    };
                }).ToArray();

                var includeScriptCompletions = node.ScopeCanReferenceScripts();

                var scriptCompletions = includeScriptCompletions
                    ? scriptFile.Program.ScriptFiles.
                    // TODO: Better strategy or configurability for completion script filtering
                    Where(s => !s.Key.ScriptName.StartsWith("TIF__", StringComparison.OrdinalIgnoreCase)).
                    Where(s => !s.Key.NamespaceParts.Any(ns => ns.CaseInsensitiveEquals("Fragments"))).
                    Select(s => new CompletionItem()
                {
                    Kind = CompletionItemKind.Class,
                    Label = s.Key,
                    SortText = $"\u2129{s.Key}"
                }).ToArray()
                : Enumerable.Empty<CompletionItem>();

                return Task.FromResult(new CompletionList(knownParamValuesCompletions.Concat(symbolCompletions.Concat(scriptCompletions))));
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<CompletionList>(null);
        }

        public void SetCapability(CompletionCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
