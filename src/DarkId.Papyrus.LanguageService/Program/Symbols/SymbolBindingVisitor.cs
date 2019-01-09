using Antlr.Runtime.Tree;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using PCompiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    class SymbolBindingVisitor : SyntaxNodeVisitor<PapyrusSymbol, PapyrusSymbol>
    {
        private readonly List<Diagnostic> _diagnostics;
        public List<Diagnostic> Diagnostic => _diagnostics;

        public SymbolBindingVisitor(List<Diagnostic> diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public override PapyrusSymbol Visit(SyntaxNode node, PapyrusSymbol parent)
        {
            var symbol = base.Visit(node, parent);

            if (symbol != null)
            {
                symbol.Definition.Symbol = symbol;
                symbol.Identifier.Symbol = symbol;

                AttachToScopeAncestor(node, symbol);
            }

            if (symbol is ScriptSymbol)
            {
                node.Children.AsParallel().ForAll(child => Visit(child, symbol ?? parent));
            }
            else
            {
                foreach (var child in node.Children)
                {
                    Visit(child, symbol ?? parent);
                }
            }

            return symbol;
        }

        private void AttachToScopeAncestor(SyntaxNode node, PapyrusSymbol symbol)
        {
            var scope = node.GetAncestors(false).OfType<ISymbolScope>().FirstOrDefault();
            if (scope != null)
            {
                node.Scope = scope;

                lock (scope.ScopedSymbols)
                {
                    if (scope.ScopedSymbols != null && !scope.ScopedSymbols.ContainsKey(symbol.Name))
                    {
                        scope.ScopedSymbols.Add(symbol.Name, symbol);
                    }
                }
            }
        }

        public override PapyrusSymbol VisitCustomEventDefinition(CustomEventDefinitionNode node, PapyrusSymbol parent)
        {
            return new CustomEventSymbol(node, parent);
        }

        public override PapyrusSymbol VisitEventDefinition(EventDefinitionNode node, PapyrusSymbol parent)
        {
            return new EventSymbol(node, parent);
        }

        public override PapyrusSymbol VisitFunctionDefinition(FunctionDefinitionNode node, PapyrusSymbol parent)
        {
            return new FunctionSymbol(node, parent);
        }

        public override PapyrusSymbol VisitFunctionParameter(FunctionParameterNode node, PapyrusSymbol parent)
        {
            return new VariableSymbol(node, parent);
        }

        public override PapyrusSymbol VisitGroupDefinition(GroupDefinitionNode node, PapyrusSymbol parent)
        {
            return new GroupSymbol(node, parent);
        }

        public override PapyrusSymbol VisitImport(ImportNode node, PapyrusSymbol parent)
        {
            return new ImportSymbol(node, parent);
        }

        public override PapyrusSymbol VisitPropertyDefinition(PropertyDefinitionNode node, PapyrusSymbol parent)
        {
            return new PropertySymbol(node, parent);
        }

        public override PapyrusSymbol VisitScript(ScriptNode node, PapyrusSymbol parent)
        {
            var script = new ScriptSymbol(node);

            if (!script.Definition.ScopedSymbols.ContainsKey("self"))
            {
                script.Definition.ScopedSymbols.Add("self", new AliasedSymbol("self", SymbolKinds.Variable, script, null));
            }

            if (script.ExtendedScript != null && !script.Definition.ScopedSymbols.ContainsKey("parent"))
            {
                script.Definition.ScopedSymbols.Add("parent", new AliasedSymbol("parent", SymbolKinds.Variable, script.ExtendedScript, null));
            }

            return script;
        }

        public override PapyrusSymbol VisitStateDefinition(StateDefinitionNode node, PapyrusSymbol parent)
        {
            return new StateSymbol(node, parent);
        }

        public override PapyrusSymbol VisitStructDefinition(StructDefinitionNode node, PapyrusSymbol parent)
        {
            return new StructSymbol(node, parent);
        }

        public override PapyrusSymbol VisitVariableDefinition(VariableDefinitionNode node, PapyrusSymbol parent)
        {
            return new VariableSymbol(node, parent);
        }

        public override PapyrusSymbol VisitDeclareStatement(DeclareStatementNode node, PapyrusSymbol parent)
        {
            return new VariableSymbol(node, parent);
        }
    }
}
