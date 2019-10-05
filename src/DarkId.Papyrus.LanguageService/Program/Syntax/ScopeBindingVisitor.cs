//using DarkId.Papyrus.Common;
//using DarkId.Papyrus.LanguageService.Program.Syntax;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DarkId.Papyrus.LanguageService.Program.Syntax
//{
//    public class ScopeBindingVisitor : SyntaxNodeVisitor<ScriptScope, ScriptScope>
//    {
//        private readonly List<Diagnostic> _diagnostics;
//        private readonly ScriptObjectType _scriptType;

//        public List<Diagnostic> Diagnostic => _diagnostics;

//        public ScopeBindingVisitor(List<Diagnostic> diagnostics, ScriptObjectType scriptType)
//        {
//            _diagnostics = diagnostics;
//            _scriptType = scriptType;
//        }

//        public void Visit(ScriptNode node)
//        {
//            node.CompilerType = _scriptType;
//            BindDefinitionBlock(node);
//        }

//        private void BindDefinitionBlock<T>(T node)
//            where T : SyntaxNode, IDefinitionBlock
//        {
//            foreach (var stateNode in node.Children.OfType<StateDefinitionNode>())
//            {
//                BindDefinitionBlock(stateNode);
//            }

//            var stateName = (node as StateDefinitionNode)?.Identifier.Text.ToLower() ?? string.Empty;

//            foreach (var functionNode in node.Children.OfType<FunctionDefinitionNode>())
//            {
//                BindFunctionHeader(functionNode, functionNode.Header, stateName);
//                BindStatementBlocks(functionNode);
//            }

//            foreach (var eventNode in node.Children.OfType<EventDefinitionNode>())
//            {
//                BindFunctionHeader(eventNode, eventNode.Header, stateName);
//                BindStatementBlocks(eventNode);
//            }

//#if FALLOUT4
//            foreach (var structNode in node.Children.OfType<StructDefinitionNode>())
//            {
//                BindStructHeader(structNode.Header);
//            }
//#endif

//            foreach (var variableNode in node.Children.OfType<VariableDefinitionNode>())
//            {
//                BindMemberVariable(variableNode);
//            }

//            foreach (var propertyNode in node.Children.OfType<PropertyDefinitionNode>())
//            {
//                BindPropertyHeader(propertyNode.Header);
//                BindDefinitionBlock(propertyNode);
//            }
//        }

//        private void BindFunctionHeader<T>(T parent, FunctionHeaderNode header, string stateName)
//            where T : SyntaxNode, IStatementBlock
//        {
//            _scriptType.TryGetFunction(stateName, header.Identifier.Text, out var type);
//            header.CompilerType = type;

//            if (type != null)
//            {
//                parent.CompilerScope = type.FunctionScope;

//                header.Flags |= type.bGlobal ? LanguageFlags.Global : LanguageFlags.None;
//                header.Flags |= type.bNative ? LanguageFlags.Native : LanguageFlags.None;

//#if FALLOUT4
//                header.Flags |= type.bBetaOnly ? LanguageFlags.BetaOnly : LanguageFlags.None;
//                header.Flags |= type.bDebugOnly ? LanguageFlags.DebugOnly : LanguageFlags.None;
//#endif
//            }
//        }

//#if FALLOUT4
//        private void BindStructHeader(StructHeaderNode header)
//        {
//            _scriptType.TryGetStruct(header.Identifier.Text, out var type);
//            header.CompilerType = type;
//        }
//#endif

//        private void BindPropertyHeader(PropertyHeaderNode header)
//        {
//            _scriptType.TryGetProperty(header.Identifier.Text, out var type);
//            header.CompilerType = type;

//            if (type != null)
//            {
//                header.Flags |= type.IsAuto ? LanguageFlags.Auto : LanguageFlags.None;
//                // TODO: AutoReadOnly by cross referencing with the mangled variable we currently ignore
//            }
//        }

//        private void BindMemberVariable(VariableDefinitionNode node)
//        {
//            _scriptType.TryGetVariable(node.Identifier.Text, out var type);
//            node.CompilerType = type;

//#if FALLOUT4
//            if (type != null)
//            {
//                node.Flags |= type.IsConst ? LanguageFlags.Const : LanguageFlags.None;
//            }
//#endif
//        }

//        private void BindStatementBlocks<T>(T parentBlock)
//            where T : SyntaxNode, IStatementBlock
//        {
//            var childIndex = 0;

//            var parentScope = parentBlock.CompilerScope;
//            if (parentScope == null)
//            {
//                return;
//            }

//            foreach (var statement in parentBlock.Statements)
//            {
//                if (statement is IfStatementNode asIfBlock)
//                {
//                    foreach (var ifBlock in asIfBlock.Bodies)
//                    {
//                        ifBlock.CompilerScope = parentScope.Children[childIndex];
//                        BindStatementBlocks(ifBlock);
//                        childIndex++;
//                    }
//                }
//                else if (statement is WhileStatementNode asWhileBlock)
//                {
//                    asWhileBlock.CompilerScope = parentScope.Children[childIndex];
//                    childIndex++;

//                    BindStatementBlocks(asWhileBlock);
//                }
//                else if (statement is DeclareStatementNode asDeclareStatement)
//                {
//                    parentScope.TryGetVariable(asDeclareStatement.Identifier.Text, out var variableType);
//                    asDeclareStatement.CompilerType = variableType;
//                }
//            }
//        }
//    }
//}