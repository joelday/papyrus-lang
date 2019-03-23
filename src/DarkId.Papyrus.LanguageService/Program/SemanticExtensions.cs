using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.LanguageService.Program.Types;

namespace DarkId.Papyrus.LanguageService.Program
{
    public static class SemanticExtensions
    {
        public static TypeChecker GetTypeChecker(this SyntaxNode node)
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

        public static IEnumerable<ISymbolScope> GetContainingScopes(this SyntaxNode node, bool includeSelf = false)
        {
            return node.GetAncestors(true).OfType<ISymbolScope>();
        }

        public static IEnumerable<PapyrusSymbol> GetSymbolsInScope(this SyntaxNode node, bool includeSelf)
        {
            var symbols = node.GetContainingScopes(includeSelf).SelectMany(s =>
            {
                if (s is ScriptNode asScript && asScript.Symbol != null)
                {
                    return asScript.Symbol.GetScriptMemberSymbols(includeDeclaredPrivates: true);
                }

                return s.ScopedSymbols.Values;
            });

            var symbolsInScope = symbols.
                Concat(symbols.OfType<GroupSymbol>().SelectMany(g => g.Children)).Where(s => !(s is GroupSymbol)).
                Concat(node.Script.Symbol.GetImportedScripts().SelectMany(s => s.GetScriptMemberSymbols(globalOnly: true)));

            if (!node.GetContainingScopes().Any(s => s is FunctionDefinitionNode || s is EventDefinitionNode))
            {
                symbolsInScope = symbolsInScope.Where(s => s.Kind == SymbolKinds.Struct);
            }

            return symbolsInScope;
        }

        public static IEnumerable<PapyrusSymbol> GetScriptMemberSymbols(this ScriptSymbol symbol, bool declaredOnly = false, bool includeDeclaredPrivates = false, bool globalOnly = false)
        {
            var symbols = (IEnumerable<PapyrusSymbol>)symbol.Definition.ScopedSymbols.Values;

            if (!globalOnly)
            {
                var kindFilter = SymbolKinds.Property | SymbolKinds.Function | SymbolKinds.Struct | SymbolKinds.Group;
                if (includeDeclaredPrivates)
                {
                    kindFilter |= SymbolKinds.Variable | SymbolKinds.Event;
                }

                symbols = symbols.Where(s => (s.Flags & LanguageFlags.Global) == 0 && (s.Kind & kindFilter) != 0);
            }
            else
            {
                symbols = symbols.Where(s => (s.Flags & LanguageFlags.Global) != 0 && (s.Kind & SymbolKinds.Function) != 0);
            }

            if (!declaredOnly)
            {
                var extendedScript = symbol.ExtendedScript;
                if (extendedScript != null)
                {
                    symbols = symbols.Concat(extendedScript.GetScriptMemberSymbols(globalOnly: globalOnly));
                }
            }

            return symbols.DistinctBy(s => s.Name + "_" + s.Kind);
        }

        public static IEnumerable<ScriptSymbol> GetExtendedScriptChain(this ScriptSymbol symbol)
        {
            var extended = symbol?.ExtendedScript;
            while (extended != null)
            {
                yield return extended;
                extended = extended.ExtendedScript;
            }
        }

        public static IEnumerable<ScriptSymbol> GetImportedScripts(this ScriptSymbol symbol)
        {
#if FALLOUT4
            var importedScriptIdentifiers = symbol?.File.CompilerType.pImportedTypes.Keys;
#elif SKYRIM
            var importedScriptIdentifiers = symbol?.Definition.Definitions.OfType<ImportNode>().Select(n => ObjectIdentifier.Parse(n.Identifier.Text));
#endif

            return importedScriptIdentifiers?.
                Select(name => ObjectIdentifier.Parse(name)).
                Where(name => name != symbol.Id).
                Except(symbol.GetExtendedScriptChain().Select(extended => extended.Id)).
                Select(name => symbol.File.Program.ScriptFiles[name].Symbol);
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

#if FALLOUT4
            if (symbol is StructSymbol asStruct)
            {
                return asStruct.Definition.Header.Identifier.GetReferencedType();
            }
#endif

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

        public static PapyrusType GetReferencedType(this IdentifierNode node)
        {
            // Disambiguation is only necessary for structs and namespaces.
#if FALLOUT4
            var referencedTypeName = node.GetScriptFile()?.ResolveRelativeTypeName(node.Text) ?? node.Text;
#elif SKYRIM
            var referencedTypeName = node.Text;
#endif
            var isArray = (node is TypeIdentifierNode asTypeIdentifier) ? asTypeIdentifier.IsArray : false;
            return node.GetTypeChecker().GetTypeForObjectId(referencedTypeName, isArray);
        }

        public static PapyrusSymbol GetReferencedTypeSymbol(this TypeIdentifierNode node)
        {
            var type = node.GetReferencedType();
            if (type is ArrayType complexType)
            {
                type = node.GetTypeChecker().GetTypeForObjectId(complexType.ElementType);
            }

            return (type as ComplexType)?.Symbol;
        }

        public static PapyrusType GetTypeOfExpression(this ExpressionNode node)
        {
            return node.GetTypeChecker().TypeEvaluationVisitor.Visit(node);
        }

        // TODO: Could probably have more semantic checks for these:
        private static bool IsSelfIdentifierExpression(this ExpressionNode expression)
        {
            return expression.Text.CaseInsensitiveEquals("self");
        }

        private static bool IsParentIdentifierExpression(this ExpressionNode expression)
        {
            return expression.Text.CaseInsensitiveEquals("parent");
        }

        public static IEnumerable<PapyrusSymbol> GetReferencableSymbolsForMemberAccess(this MemberAccessExpressionNode memberAccessExpression)
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
                if (accessExpression == null && baseExpression is IdentifierExpressionNode accessAsIdentifier)
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
                    var memberSymbols = asScriptSymbol.GetScriptMemberSymbols(globalOnly: isGlobalCall, declaredOnly: asScriptSymbol.SyntheticArrayType != null);

                    if (!isGlobalCall)
                    {
                        if (baseExpression.IsSelfIdentifierExpression())
                        {
                            return memberSymbols.Where(s => (s.Kind & SymbolKinds.Function) != 0 || (s.Kind & SymbolKinds.Property) != 0);
                        }

                        if (baseExpression.IsParentIdentifierExpression())
                        {
                            return memberSymbols.Where(s => (s.Kind & SymbolKinds.Function) != 0);
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

        public static IEnumerable<PapyrusSymbol> GetReferencableSymbols(this SyntaxNode node)
        {
            if (node is ExpressionNode || node.Parent is ExpressionNode)
            {
                var accessExpression = node.GetMemberAccessExpression();
                if (accessExpression != null)
                {
                    return accessExpression.GetReferencableSymbolsForMemberAccess();
                }
            }

            var symbolsInScope = GetSymbolsInScope(node, node is ScriptHeaderNode || node is ScriptNode);

#if FALLOUT4
            if (node is TypeIdentifierNode)
            {
                symbolsInScope = symbolsInScope.Where(s => s is StructSymbol);
            }
#endif

            return symbolsInScope;
        }

        public static PapyrusSymbol GetDeclaredOrReferencedSymbol(this IdentifierNode node)
        {
            if (node.Symbol != null)
            {
                return node.Symbol;
            }

            if (node is TypeIdentifierNode asTypeIdentifier)
            {
                return asTypeIdentifier.GetReferencedTypeSymbol();
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
                || node is FunctionDefinitionNode
                || node is EventDefinitionNode;
        }

        public static bool ScopeCanReferenceScripts(this SyntaxNode node)
        {
            return ScopeCanReferenceScriptsInternal(node)
                || ScopeCanReferenceScriptsInternal((SyntaxNode)node.Scope);
        }

        public static Task<IEnumerable<SyntaxNode>> FindReferences(this PapyrusSymbol symbol)
        {
            return symbol.FindReferences(CancellationToken.None);
        }

        public static Task<IEnumerable<SyntaxNode>> FindReferences(this PapyrusSymbol symbol, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    var symbolScriptId = symbol.Script.Id;

                    Dictionary<ObjectIdentifier, ScriptFile> scriptFiles;
                    lock (symbol.File.Program.ScriptFiles)
                    {
                        scriptFiles = symbol.File.Program.ScriptFiles.ToDictionary();
                    }

                    return scriptFiles.Values.AsParallel().WithCancellation(cancellationToken).
                        Where(file =>
                        {
                            lock (file.Text)
                            {
                                return file.Text.Text.Contains(symbol.Name);
                            }
                        }).
                        Where(file => file.CompilerNode != null && file.CompilerNode.GetDescendants().AsParallel().WithCancellation(cancellationToken).
                            Any(node => node.Text.CaseInsensitiveEquals(symbol.Name))).
                        SelectMany(file => file.Node.GetDescendants().OfType<IdentifierNode>().Where(n => n.GetDeclaredOrReferencedSymbol() == symbol)).
                        ToArray();
                }
                catch (OperationCanceledException)
                {
                    return Enumerable.Empty<SyntaxNode>();
                }
            });
        }

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
