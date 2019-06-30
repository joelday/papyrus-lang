using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

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
                case SymbolKinds.State:
                    return CompletionItemKind.Module;
                default:
                    return CompletionItemKind.Text;
            }
        }

        private readonly ProjectManager _projectManager;
        private readonly ILogger _logger;
        private readonly DisplayTextEmitter _displayTextEmitter = new DisplayTextEmitter();

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
                TriggerCharacters = new string[] { ".", " " }
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

                if (scriptFile.Node == null)
                {
                    return HandleEmptyNodeCompletion(request, scriptFile);
                }

                if (request.Context.TriggerCharacter == " " && request.Context.TriggerKind != CompletionTriggerKind.Invoked)
                {
                    return Task.FromResult<CompletionList>(null);
                }

                var node = scriptFile.Node?.GetDescendantNodeAtPosition(request.Position.ToPosition()) ?? scriptFile.Node;
                if (node == null)
                {
                    return Task.FromResult<CompletionList>(null);
                }

                var knownParamValuesCompletionItems = GetKnownParamValuesCompletionItems(request.Position, scriptFile, out var valuesAreExclusive);
                if (valuesAreExclusive)
                {
                    return Task.FromResult(new CompletionList(knownParamValuesCompletionItems));
                }

                var referencableSymbols = node.GetReferencableSymbols();

                var symbolCompletionItems = GetSymbolCompletionItems(referencableSymbols.Where(s => s.Kind != SymbolKinds.CustomEvent));

                var eventCompletionItems = node.GetAncestors().Any(ancestor => ancestor is FunctionHeaderNode asFunctionHeader && asFunctionHeader.IsEvent) ?
                    GetEventCompletionItems(referencableSymbols.Where(s => s.Kind == SymbolKinds.Event || s.Kind == SymbolKinds.CustomEvent)) : Enumerable.Empty<CompletionItem>();

                var overrideCompletionItems = node.ScopeCanDeclareFunctions() ? GetOverrideCompletionItems(node) : Enumerable.Empty<CompletionItem>();

                var scriptCompletionItems = node.ScopeCanReferenceScripts() ? GetScriptCompletionItems(scriptFile) : Enumerable.Empty<CompletionItem>();

                var allCompletions = knownParamValuesCompletionItems
                    .Concat(eventCompletionItems)
                    .Concat(symbolCompletionItems)
                    .Concat(overrideCompletionItems)
                    .Concat(scriptCompletionItems);

                return Task.FromResult(new CompletionList(allCompletions));
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while handling request.");
            }

            return Task.FromResult<CompletionList>(null);
        }

        private IEnumerable<CompletionItem> GetOverrideCompletionItems(SyntaxNode node)
        {
            var scriptSymbol = node.GetScriptFile().Symbol;
            var stateSymbol = node is StateDefinitionNode asStateDefinition ? asStateDefinition.Symbol : scriptSymbol;

            var functionImplementations = stateSymbol.Children.Where(t => t.Kind == SymbolKinds.Function || t.Kind == SymbolKinds.Event);
            var overridableFunctions = scriptSymbol.GetExtendedScriptChain(true)
                .SelectMany((extendedScript) => extendedScript.GetScriptMemberSymbols(declaredOnly: true, includeDeclaredPrivates: true))
                .Where(s => (s.Kind == SymbolKinds.Function || s.Kind == SymbolKinds.Event) &&
                    !functionImplementations.Any((implementation) => implementation.Name.CaseInsensitiveEquals(s.Name)))
                .DistinctBy(s => s.Name + "_" + s.Kind, StringComparer.OrdinalIgnoreCase);

            return overridableFunctions.Select(symbol =>
            {
                var displayText = _displayTextEmitter.GetDisplayText(symbol);
                var fullHeaderText = _displayTextEmitter.GetFullFunctionOrEventHeaderText(symbol);

                return new CompletionItem()
                {
                    Kind = GetCompletionItemKind(symbol),
                    Label = symbol.Name + " (override)",
                    Detail = displayText.Text,
                    SortText = symbol.Name,
                    Documentation = displayText.Documentation,
                    InsertTextFormat = InsertTextFormat.Snippet,
                    InsertText = $"{fullHeaderText}\r\n\t${{0}}\r\nEnd{(symbol.Kind == SymbolKinds.Event ? "Event" : "Function")}"
                };
            }).ToArray();
        }

        private Task<CompletionList> HandleEmptyNodeCompletion(CompletionParams request, ScriptFile scriptFile)
        {
            var lineText = scriptFile.Text.GetTextInRange(new Common.Range()
            {
                Start = new Common.Position()
                {
                    Line = request.Position.Line,
                    Character = 0
                },
                End = request.Position.ToPosition()
            });

            var match = Regex.Match(lineText, @"^\s*event\s+((.*)\..*)?.*$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return Task.FromResult<CompletionList>(null);
            }

            if (match.Groups.Count == 3)
            {
                var className = match.Groups[2];
                scriptFile.Program.ScriptFiles.TryGetValue(className.Value, out var matchingFile);

                if (matchingFile != null && matchingFile.Symbol != null)
                {
                    return Task.FromResult(new CompletionList(
                        GetEventCompletionItems(matchingFile.Symbol.Children
                        .Where(s => s.Kind == SymbolKinds.Event || s.Kind == SymbolKinds.CustomEvent))));
                }
            }

            return Task.FromResult(new CompletionList(GetScriptCompletionItems(scriptFile)));
        }

        public void SetCapability(CompletionCapability capability)
        {
            capability.DynamicRegistration = true;
            capability.CompletionItem.SnippetSupport = true;
        }

        private IEnumerable<CompletionItem> GetSymbolCompletionItems(IEnumerable<PapyrusSymbol> symbols)
        {
            return symbols.Select(symbol =>
            {
                var displayText = _displayTextEmitter.GetDisplayText(symbol);

                return new CompletionItem()
                {
                    Kind = GetCompletionItemKind(symbol),
                    Label = symbol.Name,
                    Detail = displayText.Text,
                    SortText = symbol.Name,
                    Documentation = displayText.Documentation
                };
            }).ToArray();
        }

        private IEnumerable<CompletionItem> GetEventCompletionItems(IEnumerable<PapyrusSymbol> symbols)
        {
            return symbols.Select(symbol =>
            {
                var displayText = _displayTextEmitter.GetDisplayText(symbol);
                var eventSignature = _displayTextEmitter.GetEventHandlerSignatureText(symbol);

                return new CompletionItem()
                {
                    Kind = GetCompletionItemKind(symbol),
                    Label = symbol.Name,
                    Detail = displayText.Text,
                    SortText = symbol.Name,
                    Documentation = displayText.Documentation,
                    InsertTextFormat = InsertTextFormat.Snippet,
                    InsertText = $"{eventSignature}\r\n\t${{0}}\r\nEndEvent"
                };
            }).ToArray();
        }

        private IEnumerable<CompletionItem> GetKnownParamValuesCompletionItems(Position position, ScriptFile scriptFile, out bool valuesAreExclusive)
        {
            var callExpressionNode = scriptFile.Node.GetDescendantNodeOfTypeAtPosition<FunctionCallExpressionNode>(position.ToPosition());
            var callExpressionParameterIndex = callExpressionNode?.GetFunctionParameterIndexAtPosition(position.ToPosition());
            valuesAreExclusive = false;

            var knownParamValues = callExpressionParameterIndex.HasValue && callExpressionParameterIndex != -1 ?
                callExpressionNode.GetKnownParameterValueSymbols(callExpressionParameterIndex.Value, out valuesAreExclusive) : null;

            return knownParamValues != null ?
                knownParamValues.Select(symbol =>
                {
                    var displayText = _displayTextEmitter.GetDisplayText(symbol);

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
        }

        private static IEnumerable<CompletionItem> GetScriptCompletionItems(ScriptFile scriptFile)
        {
            return scriptFile.Program.ScriptFiles.
                // TODO: Better strategy or configurability for completion script filtering
                Where(s => !s.Key.ScriptName.StartsWith("TIF__", StringComparison.OrdinalIgnoreCase)).
                Where(s => !s.Key.NamespaceParts.Any(ns => ns.CaseInsensitiveEquals("Fragments"))).
                Select(s => new CompletionItem()
                {
                    Kind = CompletionItemKind.Class,
                    Label = s.Key,
                    SortText = $"\u2129{s.Key}"
                }).ToArray();
        }
    }
}
