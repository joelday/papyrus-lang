using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using DarkId.Papyrus.LanguageService.Program.Types;
using DarkId.Papyrus.LanguageService.Syntax.Legacy;
using Syntax Syntax= DarkId.Papyrus.LanguageService.Syntax.Legacy.SyntaxNode;

namespace DarkId.Papyrus.LanguageService.Program
{
    public static class SemanticExtensions
    {
        public static TypeChecker GetTypeChecker(this Syntax Syntaxnode)
        {
            return node.GetProgram().TypeChecker;
        }

        public static PapyrusType GetPapyrusType(this TypeSymbol symbol)
        {
            if (symbol is ScriptSymbol asScriptSymbol && asScriptSymbol.SyntheticArrayType != null)
            {
                return asScriptSymbol.SyntheticArrayType;
            }

            var scriptType = symbol.File.Type;

#if FALLOUT4
            if (symbol is StructSymbol asStruct)
            {
                scriptType.StructTypes.TryGetValue(symbol.Id, out var structType);
                return structType;
            }
#endif

            return scriptType;
        }

        public static IEnumerable<ISymbolScope> GetContainingScopes(this Syntax Syntaxnode, bool includeSelf = false)
        {
            return node.GetAncestors(true).OfType<ISymbolScope>();
        }

        public static IEnumerable<PapyrusSymbol> GetSymbolsInScope(this Syntax Syntaxnode, bool includeSelf)
        {
            var symbols = node.GetContainingScopes(includeSelf).SelectMany(s =>
            {
                if (s is ScriptSyntax asScript && asScript.Symbol != null)
                {
                    return asScript.Symbol.GetScriptMemberSymbols(includeDeclaredPrivates: true);
                }

                return s.ScopedSymbols.Values;
            }).ToList();

            var symbolsInScope = symbols.
                Concat(symbols.OfType<GroupSymbol>().SelectMany(g => g.Children)).Where(s => !(s is GroupSymbol)).
                Concat(node.Script.Symbol.GetImportedScripts().SelectMany(s => s.GetScriptMemberSymbols(globalOnly: true))).
                Concat(node.Script.Symbol.GetScriptMemberSymbols(globalOnly: true, declaredOnly: true));

            if (!node.GetContainingScopes().Any(s => s is FunctionDefinitionSyntax|| s is EventDefinitionNode))
            {
                symbolsInScope = symbolsInScope.Where(s => s.Kind == SymbolKinds.Struct);
            }

            return symbolsInScope;
        }

        public static IEnumerable<PapyrusSymbol> GetScriptMemberSymbols(
            this ScriptSymbol symbol,
            bool declaredOnly = false,
            bool includeDeclaredPrivates = false,
            bool globalOnly = false,
            SymbolKinds additionalNonGlobalKinds = SymbolKinds.None)
        {
            var symbols = (IEnumerable<PapyrusSymbol>)symbol.Definition.ScopedSymbols.Values;

            if (!globalOnly)
            {
                var kindFilter = SymbolKinds.Property | SymbolKinds.Function | SymbolKinds.Event | SymbolKinds.Struct | SymbolKinds.Group;
                if (includeDeclaredPrivates)
                {
                    kindFilter |= SymbolKinds.Variable;
                }

                kindFilter |= additionalNonGlobalKinds;

                symbols = symbols.Where(s => (s.Flags & LanguageFlags.Global) == 0 && (s.Kind & kindFilter) != 0);
            }
            else
            {
                symbols = symbols.Where(s => (s.Flags & LanguageFlags.Global) != 0 && (s.Kind & SymbolKinds.Function) != 0);

                if (declaredOnly)
                {
                    symbols = symbols.Where(s => s.Script == symbol.Script);
                }
            }

            if (!declaredOnly)
            {
                var extendedScript = symbol.ExtendedScript;
                if (extendedScript != null)
                {
                    symbols = symbols.Concat(extendedScript.GetScriptMemberSymbols(globalOnly: globalOnly));
                }
            }

            return symbols.DistinctBy(s => s.Name + "_" + s.Kind, StringComparer.OrdinalIgnoreCase);
        }

        public static IEnumerable<ScriptSymbol> GetExtendedScriptChain(this ScriptSymbol symbol, bool includeSelf = false)
        {
            if (includeSelf)
            {
                yield return symbol;
            }

            var extended = symbol?.ExtendedScript;
            while (extended != null)
            {
                yield return extended;
                extended = extended.ExtendedScript;
            }
        }

        public static IEnumerable<ScriptSymbol> GetImportedScripts(this ScriptSymbol symbol)
        {
//#if FALLOUT4
//            var importedScriptIdentifiers = symbol?.File.CompilerType.pImportedTypes.Keys;
//#elif SKYRIM
//            var importedScriptIdentifiers = symbol?.Definition.Definitions.OfType<ImportNode>().Select(n => ObjectIdentifier.Parse(n.Identifier.Text));
//#endif

//            return importedScriptIdentifiers?.
//                Select(name => ObjectIdentifier.Parse(name)).
//                Where(name => name != symbol.Id).
//                Except(symbol.GetExtendedScriptChain().Select(extended => extended.Id)).
//                Select(name => symbol.File.Program.ScriptFiles[name].Symbol);

            return Enumerable.Empty<ScriptSymbol>();
        }

        public static PapyrusType GetPapyrusType(this PapyrusSymbol symbol)
        {
            if (symbol is AliasedSymbol asAliased)
            {
                return asAliased.Aliased.GetPapyrusType();
            }

            if (symbol is ScriptSymbol asScript)
            {
                return (ComplexType)asScript.File?.Type ?? asScript.SyntheticArrayType;
            }

            if (symbol is StructSymbol asStruct)
            {
                return asStruct.Definition.Header.Identifier.GetReferencedType();
            }

            if (symbol is FunctionSymbol asFunction)
            {
                return asFunction.Definition.Header.TypeIdentifier?.GetReferencedType();
            }

            if (symbol is PropertySymbol asProperty)
            {
                return asProperty.Definition.Header.TypeIdentifier?.GetReferencedType();
            }

            if (symbol.Definition is ITypeIdentifiable asTypeIdentifiable)
            {
                return asTypeIdentifiable.TypeIdentifier?.GetReferencedType();
            }

            return null;
        }

        public static PapyrusType GetReferencedType(this IdentifierSyntaxNode)
        {
            // Disambiguation is only necessary for structs and namespaces.
            var referencedTypeName = node.GetScriptFile()?.ResolveRelativeTypeName(node.Text) ?? node.Text;

            var isArray = (node is TypeIdentifierSyntax asTypeIdentifier) ? asTypeIdentifier.IsArray : false;
            return node.GetTypeChecker().GetTypeForObjectId(referencedTypeName, isArray);
        }

        public static PapyrusSymbol GetReferencedTypeSymbol(this TypeIdentifierSyntaxNode)
        {
            var type = node.GetReferencedType();
            if (type is ArrayType complexType)
            {
                type = node.GetTypeChecker().GetTypeForObjectId(complexType.ElementType);
            }

            return (type as ComplexType)?.Symbol;
        }

        public static PapyrusType GetTypeOfExpression(this ExpressionSyntaxNode)
        {
            return node.GetTypeChecker().TypeEvaluationVisitor.Visit(node);
        }

        // TODO: Could probably have more semantic checks for these:
        private static bool IsSelfIdentifierExpression(this ExpressionSyntax expression)
        {
            return expression.Text.CaseInsensitiveEquals("self");
        }

        private static bool IsParentIdentifierExpression(this ExpressionSyntax expression)
        {
            return expression.Text.CaseInsensitiveEquals("parent");
        }

        public static IEnumerable<PapyrusSymbol> GetReferencableSymbolsForMemberAccess(this MemberAccessExpressionSyntax memberAccessExpression)
        {
            var baseExpression = memberAccessExpression.BaseExpression;
            var accessExpression = memberAccessExpression.AccessExpression;

            var baseType = baseExpression.GetTypeOfExpression();
            if (baseType is ComplexType asComplexType)
            {
                var functionCall = accessExpression?.GetDescendants(true).OfType<FunctionCallExpressionNode>().FirstOrDefault();
                var isGlobalCall = functionCall != null && functionCall.IsGlobalCall;

                // TODO: Unsure if this still needs to be done.
                // If the accessExpression is null (we're at '.' without any identifier), we need to fall back to inferring whether
                // or not this is a global call by looking at the base expression.
                // If it is an identifier expression that resolves to a script type, then we know the member symbols are global only.
                if (accessExpression == null && baseExpression is IdentifierExpressionSyntax accessAsIdentifier)
                {
                    var referencedSymbol = accessAsIdentifier.Identifier.GetDeclaredOrReferencedSymbol();
                    if (referencedSymbol != null)
                    {
                        isGlobalCall = referencedSymbol.Kind == SymbolKinds.Script;
                    }
                }

                var typeSymbol = asComplexType.Symbol;
                if (typeSymbol is ScriptSymbol asScriptSymbol)
                {
                    var memberSymbols = asScriptSymbol.GetScriptMemberSymbols(
                        globalOnly: isGlobalCall,
                        declaredOnly: asScriptSymbol.SyntheticArrayType != null,
                        additionalNonGlobalKinds: memberAccessExpression.Parent is FunctionHeaderSyntax? SymbolKinds.Event | SymbolKinds.CustomEvent : SymbolKinds.None);

                    if (memberAccessExpression.Parent is FunctionHeaderNode)
                    {
                        return memberSymbols.Where(s => (s.Kind & SymbolKinds.Event) != 0 || (s.Kind & SymbolKinds.CustomEvent) != 0);
                    }

                    if (!isGlobalCall)
                    {
                        if (baseExpression.IsSelfIdentifierExpression())
                        {
                            return memberSymbols.Where(s => (s.Kind & SymbolKinds.Function) != 0 || (s.Kind & SymbolKinds.Event) != 0 || (s.Kind & SymbolKinds.Property) != 0);
                        }

                        if (baseExpression.IsParentIdentifierExpression())
                        {
                            return memberSymbols.Where(s => (s.Kind & SymbolKinds.Function) != 0 || (s.Kind & SymbolKinds.Event) != 0);
                        }
                    }

                    return memberSymbols;
                }

#if FALLOUT4
                if (typeSymbol is StructSymbol asStructSymbol)
                {
                    return asStructSymbol.Children;
                }
#endif
            }

            return Enumerable.Empty<PapyrusSymbol>();
        }

        public static IEnumerable<PapyrusSymbol> GetReferencableSymbols(this Syntax Syntaxnode)
        {
            if (node is ExpressionSyntax|| node.Parent is ExpressionNode)
            {
                var accessExpression = node.GetMemberAccessExpression();
                if (accessExpression != null)
                {
                    return accessExpression.GetReferencableSymbolsForMemberAccess();
                }
            }

            // Declaration identifiers shouldn't return any referencable symbols.
            if (node is IdentifierSyntax&&
                (node.Parent is DeclareStatementSyntax|| node.Parent is FunctionParameterNode))
            {
                return Enumerable.Empty<PapyrusSymbol>();
            }

#if FALLOUT4
            // TODO: Double check on this after resolving remaining issues.
            // For remote event handler definitions with an empty access expression, this needs to be redirected.
            if (node is FunctionHeaderSyntax asFunctionHeaderNode &&
                asFunctionHeaderNode.Parent is EventDefinitionSyntax&&
                asFunctionHeaderNode.RemoteEventExpression != null &&
                asFunctionHeaderNode.RemoteEventExpression.AccessExpression.Text == string.Empty)
            {
                return asFunctionHeaderNode.RemoteEventExpression.AccessExpression.GetReferencableSymbols();
            }
#endif

            if (!(node is ScriptHeaderNode) && node is ITypedIdentifiable typed && typed.TypeIdentifier?.Text != string.Empty)
            {
                return Enumerable.Empty<PapyrusSymbol>();
            }

            // TODO: Error representation
            //if (node.CompilerNode is CommonErrorNode)
            //{
            //    return Enumerable.Empty<PapyrusSymbol>();
            //}

            var symbolsInScope = GetSymbolsInScope(node, node is ScriptHeaderSyntax|| node is ScriptNode);

#if FALLOUT4
            if (node is TypeIdentifierNode)
            {
                symbolsInScope = symbolsInScope.Where(s => s is StructSymbol);
            }
#endif

            return symbolsInScope;
        }

        public static PapyrusSymbol GetDeclaredOrReferencedSymbol(this IdentifierSyntaxNode)
        {
            if (node.Symbol != null)
            {
                return node.Symbol;
            }

            if (node is TypeIdentifierSyntax asTypeIdentifier)
            {
                return asTypeIdentifier.GetReferencedTypeSymbol();
            }

            if (node is IdentifierSyntax&& node.Parent is FunctionCallExpressionParameterSyntax callParameterNode)
            {
                var parameterSyntax= callParameterNode.GetParameterDefinition();
                return parameterNode.Identifier.GetDeclaredOrReferencedSymbol();
            }

            var referenced = node.GetReferencableSymbols().FirstOrDefault(n => n.Name.CaseInsensitiveEquals(node.Text));
            if (referenced != null)
            {
                return referenced;
            }

            return (node.GetReferencedType() as ComplexType)?.Symbol;
        }

        private static bool ScopeCanReferenceScriptsInternal(SyntaxNode node)
        {
            return node is ScriptNode
                || node is ScriptHeaderNode
                || node is TypeIdentifierNode
                || node is IDefinitionBlock
                || node is IStatementBlock
                // These conditions allow incomplete variable declarations to suggest types:
                || (node is IdentifierSyntax&&
                    node.Parent is IdentifierExpressionSyntax&&
                    node.Parent?.Parent is ExpressionStatementSyntax&&
                    node.Parent?.Parent?.Parent is IStatementBlock);
        }

        public static bool ScopeCanReferenceScripts(this Syntax Syntaxnode)
        {
            return ScopeCanReferenceScriptsInternal(node)
                || ScopeCanReferenceScriptsInternal((SyntaxNode)node.Scope);
        }

        public static bool ScopeCanDeclareFunctions(this Syntax Syntaxnode)
        {
            return node is ScriptSyntax|| node is StateDefinitionNode;
        }

        public static FunctionDefinitionSyntax GetDefinition(this FunctionCallExpressionSyntax callExpression)
        {
            return (callExpression.Identifier.GetDeclaredOrReferencedSymbol() as FunctionSymbol)?.Definition;
        }

        public static FunctionParameterSyntax GetParameterDefinition(this FunctionCallExpressionSyntax callExpression, int parameterIndex)
        {
            var functionDefinition = callExpression.GetDefinition();
            if (functionDefinition == null)
            {
                return null;
            }

            return functionDefinition.Header.Parameters.ElementAtOrDefault(parameterIndex);
        }

        public static FunctionParameterSyntax GetParameterDefinition(this FunctionCallExpressionParameterSyntax callParameterNode)
        {
            var callExpression = ((FunctionCallExpressionNode)callParameterNode.Parent);

            if (callParameterNode.Identifier != null)
            {
                return callExpression.GetDefinition().Header.Parameters.FirstOrDefault(p =>
                    p.Identifier.Text.CaseInsensitiveEquals(callParameterNode.Identifier.Text));
            }

            var parameterIndex = callExpression.Parameters.IndexOf(callParameterNode);
            return callExpression.GetParameterDefinition(parameterIndex);
        }

        public static IEnumerable<PapyrusSymbol> GetKnownParameterValueSymbols(this FunctionCallExpressionSyntax functionCallExpression, int parameterIndex, out bool valuesAreValidExclusively)
        {
            valuesAreValidExclusively = false;

            var definedFunction = functionCallExpression.GetDefinition();
            var parameterDefinition = functionCallExpression.GetParameterDefinition(parameterIndex);

#if FALLOUT4
            if (parameterDefinition != null)
            {
                var parameterTypeName = parameterDefinition.TypeIdentifier.Text;

                if (parameterTypeName.CaseInsensitiveEquals("CustomEventName"))
                {
                    valuesAreValidExclusively = true;
                    return GetKnownEventParameterSymbols<CustomEventSymbol>(functionCallExpression, parameterIndex);
                }

                if (parameterTypeName.CaseInsensitiveEquals("ScriptEventName"))
                {
                    valuesAreValidExclusively = true;
                    return GetKnownEventParameterSymbols<EventSymbol>(functionCallExpression, parameterIndex);
                }

                var functionName = definedFunction.Header.Identifier.Text;
                if (functionName.CaseInsensitiveEquals("FindStruct") || functionName.CaseInsensitiveEquals("RFindStruct"))
                {
                    if (parameterDefinition.Identifier.Text.CaseInsensitiveEquals("asVarName"))
                    {
                        valuesAreValidExclusively = true;

                        var definedFunctionScriptSymbol = definedFunction.Symbol.Script;
                        var elementTypeId = definedFunctionScriptSymbol.SyntheticArrayType?.ElementType;
                        if (elementTypeId.HasValue)
                        {
                            if (functionCallExpression.GetTypeChecker().GetTypeForObjectId(elementTypeId.Value) is StructType elementType)
                            {
                                return elementType.Symbol.Children;
                            }
                        }

                        return Enumerable.Empty<PapyrusSymbol>();
                    }
                }
            }
#endif

            if (definedFunction.Symbol.Name.CaseInsensitiveEquals("GotoState"))
            {
                var expressionForScriptType = functionCallExpression.GetMemberAccessExpression()?.BaseExpression;
                var scriptType = expressionForScriptType?.GetTypeOfExpression() as ScriptType ?? functionCallExpression.Script.Symbol.GetPapyrusType() as ScriptType;

                return scriptType.Symbol.GetExtendedScriptChain(true).
                    SelectMany(scriptSymbol => scriptSymbol.Children.OfType<StateSymbol>()).
                    DistinctBy(s => s.Name.ToLower()).ToArray();
            }

            return Enumerable.Empty<PapyrusSymbol>();
        }

#if FALLOUT4
        private static IEnumerable<PapyrusSymbol> GetKnownEventParameterSymbols<T>(FunctionCallExpressionNode functionCallExpression, int parameterIndex)
            where T : PapyrusSymbol
        {
            // If the event name parameter is not the first parameter:
            // We assume that the first parameter's expression result is of the source type.

            // If it *is* the first parameter:
            // The defined function most likely lives on ScriptObject, so we need to find the derived type that is the subject of the call.
            // So, either the source type is the base expression of the parent member access expression, -or-, if there is no member access expression,
            // then it must be the current script type.

            var expressionForSourceType = parameterIndex > 0 ?
                functionCallExpression.Parameters.ElementAtOrDefault(0).Value : functionCallExpression.GetMemberAccessExpression()?.BaseExpression;

            var sourceType = expressionForSourceType?.GetTypeOfExpression() as ScriptType ?? functionCallExpression.Script.Symbol.GetPapyrusType() as ScriptType;

            // CustomEventNames can be inherited, so the symbols are sourced from the full extended script chain
            return sourceType.Symbol.GetExtendedScriptChain(true).SelectMany(scriptSymbol => scriptSymbol.Children.OfType<T>()).ToArray();
        }
#endif

        // TODO: Move these elsewhere?
        public static Task<IEnumerable<SyntaxNode>> FindReferences(this PapyrusSymbol symbol)
        {
            return symbol.FindReferences(CancellationToken.None);
        }

        public static Task<IEnumerable<SyntaxNode>> FindReferences(this PapyrusSymbol symbol, CancellationToken cancellationToken)
        {
            //return Task.Run(() =>
            //{
            //    try
            //    {
            //        var symbolScriptId = symbol.Script.Id;

            //        Dictionary<ObjectIdentifier, ScriptFile> scriptFiles;
            //        lock (symbol.File.Program.ScriptFiles)
            //        {
            //            scriptFiles = symbol.File.Program.ScriptFiles.ToDictionary();
            //        }

            //        return scriptFiles.Values.AsParallel().WithCancellation(cancellationToken).
            //            Where(file =>
            //            {
            //                lock (file.Text)
            //                {
            //                    return file.Text.Text.Contains(symbol.Name);
            //                }
            //            }).
            //            Where(file => file.CompilerNode != null && file.CompilerNode.GetDescendants().AsParallel().WithCancellation(cancellationToken).
            //                Any(node => node.Text.CaseInsensitiveEquals(symbol.Name))).
            //            SelectMany(file => file.Node.GetDescendants().OfType<IdentifierNode>().Where(n => n.GetDeclaredOrReferencedSymbol() == symbol)).
            //            ToArray();
            //    }
            //    catch (OperationCanceledException)
            //    {
            //        return Enumerable.Empty<SyntaxNode>();
            //    }
            //});

            throw new NotImplementedException();
        }

        // TODO: Move this elsewhere?
        public static string GetDocumentationMarkdown(this PapyrusSymbol symbol)
        {
            var docs = symbol.Documentation.
                Where(doc => !string.IsNullOrEmpty(doc.Trim())).
                Select(doc =>
                    doc.Split('\n').Select(d => d.TrimEnd() + "  ").Join("\r\n")).
                Join("\r\n___\r\n");

            if (string.IsNullOrWhiteSpace(docs))
            {
                return null;
            }

            return docs;
        }
    }
}
