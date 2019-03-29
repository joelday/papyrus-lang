using Antlr.Runtime;
using Antlr.Runtime.Tree;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.External;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Program.Syntax
{
    class NodeBinder
    {
        private readonly object _definitionsLock = new object();

        private IReadOnlyScriptText _text;
        private ITokenStream _tokenStream;
        private List<Diagnostic> _diagnostics;

        private ScriptNode _scriptNode;

        public DiagnosticResult<ScriptNode> Bind(ScriptFile file, PapyrusProgram program, IReadOnlyScriptText text, ITokenStream tokenStream, CommonTree objectNode)
        {
            return DiagnosticResult<ScriptNode>.TryWithDiagnostics((diagnostics) =>
            {
                _text = text;
                _tokenStream = tokenStream;
                _diagnostics = diagnostics;

                _scriptNode = new ScriptNode
                {
                    File = file,
                    Program = program
                };
                _scriptNode.Script = _scriptNode;
                _scriptNode.CompilerNode = objectNode;
                _scriptNode.Range = objectNode.GetRange(tokenStream, text);
                _scriptNode.Text = text.Text;

                var children = new Scanner<CommonTree>(objectNode.GetChildren());
                children.Next();

                _scriptNode.Header = BindScriptHeader(_scriptNode, children);

                foreach (var child in children.AllRemaining())
                {
                    var blockChildren = new Scanner<CommonTree>(new CommonTree[] { child });
                    blockChildren.Next();

                    var definition = BindDefinition(_scriptNode, blockChildren);
                    if (definition != null)
                    {
                        lock (_definitionsLock)
                        {
                            _scriptNode.Definitions.Add(definition);
                        }
                    }
                }

                return _scriptNode;
            });
        }

        #region Definitions


        private ScriptHeaderNode BindScriptHeader(ScriptNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<ScriptHeaderNode>(parent, parentChildren, (node, children) =>
            {
                node.Identifier = BindIdentifier(node, parentChildren);

                children.Next();

                if (children.PeekType() == AstType.Identifier)
                {
                    children.Next();
                    node.TypeIdentifier = BindTypeIdentifier(node, children);
                }

                BindRemainingStandardDefinitionNodes(node, children);
            });
        }

        private void BindRemainingStandardDefinitionNodes(SyntaxNode node, Scanner<CommonTree> children)
        {
            while (children.Next())
            {
                switch (children.Current.GetAstType())
                {
                    case AstType.DocString:
                        if (node is IDocumentable asDocumentable)
                        {
                            asDocumentable.DocComment = BindDocComment(node, children);
                        }
                        break;
                }
            }
        }

        private SyntaxNode BindDefinition(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            switch (parentChildren.Current.GetAstType())
            {
                case AstType.Property:
                case AstType.AutoProperty:
                    return BindProperty(parent, parentChildren);
#if FALLOUT4
                case AstType.CustomEvent:
                    return BindCustomEvent(parent, parentChildren);
#endif
                case AstType.Event:
#if FALLOUT4
                case AstType.RemoteEvent:
#endif
                    return BindEvent(parent, parentChildren);
                case AstType.Function:
                    return BindFunction(parent, parentChildren);
#if FALLOUT4
                case AstType.Group:
                    return BindGroup(parent, parentChildren);
#endif
                case AstType.Import:
                    // TODO: AST is missing for imports for FO4
                    return BindImport(parent, parentChildren);
                case AstType.State:
                    return BindState(parent, parentChildren);
#if FALLOUT4
                case AstType.Struct:
                    return BindStruct(parent, parentChildren);
#endif
                case AstType.Variable:
                    return BindVariable(parent, parentChildren);
                default:
                    break;
            }

            return null;
        }

        private FunctionDefinitionNode BindFunction(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<FunctionDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Header = BindFunctionHeader(node, children);

                children.Next();
                BindStatementsToBlock(node, children);
            });
        }

        private EventDefinitionNode BindEvent(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<EventDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Header = BindFunctionHeader(node, children);

                children.Next();
                BindStatementsToBlock(node, children);
            });
        }

        private FunctionHeaderNode BindFunctionHeader(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<FunctionHeaderNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.Identifier = BindIdentifier(node, children);

                children.Next();

                while (children.Next() && children.Current.GetAstType() == AstType.Parameter)
                {
                    node.Parameters.Add(BindFunctionParameter(node, children));
                }

                BindRemainingStandardDefinitionNodes(node, children);
            });
        }

        private FunctionParameterNode BindFunctionParameter(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<FunctionParameterNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.Identifier = BindIdentifier(node, children);
            });
        }

        private CustomEventDefinitionNode BindCustomEvent(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<CustomEventDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Identifier = BindIdentifier(node, children);
            });
        }

        private PropertyDefinitionNode BindProperty(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<PropertyDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Header = BindPropertyHeader(node, children);

                while (children.Next())
                {
                    if (children.Current.GetAstType() != AstType.Function)
                    {
                        continue;
                    }

                    BindDefinitionsToBlock(node, children);
                }
            });
        }

#if FALLOUT4
        private StructDefinitionNode BindStruct(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<StructDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Header = BindStructHeader(node, children);

                BindDefinitionsToBlock(node, children);
            });
        }

        private StructHeaderNode BindStructHeader(StructDefinitionNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<StructHeaderNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Identifier = BindIdentifier(node, children);
            });
        }
#endif

        private void BindDefinitionsToBlock(IDefinitionBlock parent, Scanner<CommonTree> parentChildren)
        {
            while (parentChildren.Next())
            {
                var definition = BindDefinition((SyntaxNode)parent, parentChildren);
                if (definition != null)
                {
                    parent.Definitions.Add(definition);
                }
            }
        }

        private PropertyHeaderNode BindPropertyHeader(PropertyDefinitionNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<PropertyHeaderNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.Identifier = BindIdentifier(node, children);

                BindRemainingStandardDefinitionNodes(node, children);
            });
        }

        private GroupDefinitionNode BindGroup(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<GroupDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Header = BindGroupHeader(node, children);

                BindDefinitionsToBlock(node, children);
            });
        }

        private ImportNode BindImport(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<ImportNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Identifier = BindIdentifier(node, children);
            });
        }

        private StateDefinitionNode BindState(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<StateDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Identifier = BindIdentifier(node, children);

                BindDefinitionsToBlock(node, children);
            });
        }

        private GroupHeaderNode BindGroupHeader(GroupDefinitionNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<GroupHeaderNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Identifier = BindIdentifier(node, children);
            });
        }

        private IdentifierNode BindIdentifier(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<IdentifierNode>(parent, parentChildren, (node, children) =>
            {
                node.Range = new Range()
                {
                    Start = new Position()
                    {
                        Character = node.CompilerNode.CharPositionInLine,
                        Line = node.CompilerNode.Line - 1
                    },
                    End = new Position()
                    {
                        Character = node.CompilerNode.CharPositionInLine + node.CompilerNode.Text.Length,
                        Line = node.CompilerNode.Line - 1
                    }
                };

                node.Text = node.CompilerNode.Text;
            });
        }

        private TypeIdentifierNode BindTypeIdentifier(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            if (parentChildren.Current.GetAstType() == AstType.None)
            {
                return null;
            }

            return CreateNode<TypeIdentifierNode>(parent, parentChildren, (node, children) =>
            {
                node.Text = _text.GetTextInRange(node.Range);

                if (!node.Text.CaseInsensitiveEquals("CustomEventName"))
                {
                    node.Range = new Range()
                    {
                        Start = new Position()
                        {
                            Character = node.CompilerNode.CharPositionInLine,
                            Line = node.CompilerNode.Line - 1
                        },
                        End = new Position()
                        {
                            Character = node.CompilerNode.CharPositionInLine + node.CompilerNode.Text.Length,
                            Line = node.CompilerNode.Line - 1
                        }
                    };

                    node.Text = _text.GetTextInRange(node.Range);
                }

                while (parentChildren.PeekType() == AstType.LeftBracket || parentChildren.PeekType() == AstType.RightBracket)
                {
                    node.IsArray = true;
                    parentChildren.Next();
                }
            });
        }

        private DocCommentNode BindDocComment(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<DocCommentNode>(parent, parentChildren);
        }

        private VariableDefinitionNode BindVariable(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            var identifierElements = parentChildren.Current.GetChildren().Where(e => e.GetAstType() == AstType.Identifier);
            if (identifierElements.Any(e => e.Text.StartsWith("::")))
            {
                return null;
            }

            return CreateNode<VariableDefinitionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.Identifier = BindIdentifier(node, children);

                BindRemainingStandardDefinitionNodes(node, children);
            });
        }

        #endregion

        #region Statements

        private SyntaxNode BindStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            var astType = parentChildren.Current.GetAstType();
            if (astType.ToAssignmentOperatorType() != AssignmentOperatorType.None)
            {
                return BindAssignmentStatement(parent, parentChildren);
            }

            switch (astType)
            {
                case AstType.While:
                    return BindWhileStatement(parent, parentChildren);
                case AstType.If:
                    return BindIfStatement(parent, parentChildren);
                case AstType.Variable:
                    return BindDeclareStatement(parent, parentChildren);
                case AstType.Return:
                    return BindReturnStatement(parent, parentChildren);
                default:
                    return BindExpressionStatement(parent, parentChildren);
            }
        }

        private void BindStatementsToBlock(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            if (parentChildren.Done)
            {
                return;
            }

            var blockChildren = new Scanner<CommonTree>(parentChildren.Current.GetChildren());

            while (blockChildren.Next())
            {
                var statement = BindStatement(parent, blockChildren);
                if (statement != null)
                {
                    parent.Statements.Add(statement);
                }
            }
        }

        private ExpressionStatementNode BindExpressionStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<ExpressionStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                node.Expression = BindExpression(node, parentChildren);
            });
        }

        private AssignmentStatementNode BindAssignmentStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<AssignmentStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                node.Operation = node.CompilerNode.GetAstType().ToAssignmentOperatorType();

                children.Next();
                node.LeftValue = BindExpression(node, children);

                children.Next();
                node.RightValue = BindExpression(node, children);
            });
        }

        private IfStatementNode BindIfStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<IfStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                node.Bodies.Add(BindIfStatementBody(node, parentChildren));

                children.Next();
                children.Next();

                while (children.Next())
                {
                    node.Bodies.Add(BindIfStatementBody(node, children));
                }
            });
        }

        private IfStatementBodyNode BindIfStatementBody(IfStatementNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<IfStatementBodyNode>(parent, parentChildren, (node, children) =>
            {
                switch (node.CompilerNode.GetAstType())
                {
                    case AstType.If:
                        node.BodyKind = IfStatementBodyKind.If;
                        break;
                    case AstType.ElseIf:
                        node.BodyKind = IfStatementBodyKind.ElseIf;
                        break;
                    case AstType.Else:
                        node.BodyKind = IfStatementBodyKind.Else;
                        break;
                }

                if (node.BodyKind != IfStatementBodyKind.Else)
                {
                    children.Next();
                    node.Condition = BindExpression(node, children);
                }

                children.Next();
                BindStatementsToBlock(node, children);
            });
        }

        private WhileStatementNode BindWhileStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<WhileStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.Expression = BindExpression(node, children);

                children.Next();
                BindStatementsToBlock(node, children);
            });
        }

        private ReturnStatementNode BindReturnStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<ReturnStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                children.Next();

                if (!children.Done)
                {
                    node.ReturnValue = BindExpression(node, children);
                }
            });
        }

        private DeclareStatementNode BindDeclareStatement(IStatementBlock parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<DeclareStatementNode>((SyntaxNode)parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.Identifier = BindIdentifier(node, children);

                if (children.Next())
                {
                    node.InitialValue = BindExpression(node, children);
                }
            });
        }

        #endregion

        #region Expressions

        private ExpressionNode BindExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            if (parentChildren.Done)
            {
                return null;
            }

            var astType = parentChildren.Current.GetAstType();

            if (astType.ToUnaryOperatorType() != UnaryOperatorType.None)
            {
                return BindUnaryOperationExpression(parent, parentChildren);
            }

            if (astType.ToBinaryOperatorType() != BinaryOperatorType.None)
            {
                return BindBinaryOperationExpression(parent, parentChildren);
            }

            switch (parentChildren.Current.GetAstType())
            {
                case AstType.Call:
                case AstType.CallGlobal:
                case AstType.CallParent:
#if FALLOUT4
                case AstType.ArrayAdd:
                case AstType.ArrayClear:
#endif
                case AstType.ArrayFind:
#if FALLOUT4
                case AstType.ArrayFindStruct:
                case AstType.ArrayInsert:
                case AstType.ArrayRemove:
                case AstType.ArrayRemoveLast:
#endif
                case AstType.ArrayRFind:
#if FALLOUT4
                case AstType.ArrayRFindStruct:
#endif
                    return BindFunctionCallExpression(parent, parentChildren);
                case AstType.Identifier:
                    return BindIdentifierExpression(parent, parentChildren);
                case AstType.Dot:
                    return BindMemberAccessExpression(parent, parentChildren);
#if FALLOUT4
                case AstType.NewArray:
                    return BindNewArrayExpression(parent, parentChildren);
                case AstType.NewStruct:
                    return BindNewStructExpression(parent, parentChildren);
#endif
                case AstType.ArrayGet:
                case AstType.ArraySet:
                    return BindArrayIndexExpression(parent, parentChildren);
                case AstType.Integer:
                case AstType.Bool:
                case AstType.Float:
                case AstType.HexDigit:
                case AstType.String:
                case AstType.None:
                    return BindLiteralExpression(parent, parentChildren);
                case AstType.As:
                    return BindCastExpression(parent, parentChildren);
#if FALLOUT4
                case AstType.Is:
                    return BindIsExpression(parent, parentChildren);
#endif
                case AstType.ParameterExpression:
                    var innerChildren = new Scanner<CommonTree>(parentChildren.Current.GetChildren());
                    innerChildren.Next();

                    return BindExpression(parent, innerChildren);
            }

            return null;
        }

        private LiteralExpressionNode BindLiteralExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<LiteralExpressionNode>(parent, parentChildren, (node, children) =>
            {
                var valueText = node.CompilerNode.Text;

                switch (node.CompilerNode.GetAstType())
                {
                    case AstType.Integer:
                        node.Value = CreateNode<IntLiteralNode>(node, parentChildren, (literalNode, _) => literalNode.Value = int.Parse(valueText));
                        break;
                    case AstType.Bool:
                        node.Value = CreateNode<BoolLiteralNode>(node, parentChildren, (literalNode, _) => literalNode.Value = bool.Parse(valueText));
                        break;
                    case AstType.Float:
                        node.Value = CreateNode<FloatLiteralNode>(node, parentChildren, (literalNode, _) => literalNode.Value = float.Parse(valueText));
                        break;
                    case AstType.HexDigit:
                        node.Value = CreateNode<HexLiteralNode>(node, parentChildren, (literalNode, _) => literalNode.Value = Convert.ToInt32(valueText, 16));
                        break;
                    case AstType.String:
                        node.Value = CreateNode<StringLiteralNode>(node, parentChildren, (literalNode, _) => literalNode.Value = valueText);
                        break;
                    case AstType.None:
                        node.Value = CreateNode<NoneLiteralNode>(node, parentChildren);
                        break;
                }
            });
        }

        private CastExpressionNode BindCastExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<CastExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.InnerExpression = BindExpression(node, children);

                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);
            });
        }

        private IsExpressionNode BindIsExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<IsExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.InnerExpression = BindExpression(node, children);

                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);
            });
        }

        private NewArrayExpressionNode BindNewArrayExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<NewArrayExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);

                children.Next();
                node.LengthExpression = BindExpression(node, children);
            });
        }

        private NewStructExpressionNode BindNewStructExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<NewStructExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.TypeIdentifier = BindTypeIdentifier(node, children);
            });
        }

        private ArrayIndexExpressionNode BindArrayIndexExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<ArrayIndexExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.ArrayExpression = BindExpression(node, children);

                children.Next();
                node.IndexExpression = BindExpression(node, children);
            });
        }

        private UnaryOperationExpressionNode BindUnaryOperationExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<UnaryOperationExpressionNode>(parent, parentChildren, (node, children) =>
            {
                node.Operator = node.CompilerNode.GetAstType().ToUnaryOperatorType();

                children.Next();
                node.InnerExpression = BindExpression(node, children);
            });
        }

        private BinaryOperationExpressionNode BindBinaryOperationExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<BinaryOperationExpressionNode>(parent, parentChildren, (node, children) =>
            {
                node.Operator = node.CompilerNode.GetAstType().ToBinaryOperatorType();

                children.Next();
                node.Left = BindExpression(node, children);

                children.Next();
                node.Right = BindExpression(node, children);
            });
        }

        private FunctionCallExpressionNode BindFunctionCallExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<FunctionCallExpressionNode>(parent, parentChildren, (node, children) =>
            {
                node.IsGlobalCall = parentChildren.Current.GetAstType() == AstType.CallGlobal;

                children.Next();
                node.Identifier = BindIdentifier(node, children);

                children.Next();
                var parameterNodes = new Scanner<CommonTree>(children.Current.GetChildren());

                while (parameterNodes.Next())
                {
                    var parameter = BindFunctionCallExpressionParameter(node, parameterNodes);
                    if (parameter != null)
                    {
                        node.Parameters.Add(parameter);
                    }
                }
            });
        }

        private FunctionCallExpressionParameterNode BindFunctionCallExpressionParameter(FunctionCallExpressionNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<FunctionCallExpressionParameterNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();

                if (children.Current.Text != string.Empty)
                {
                    node.Identifier = BindIdentifier(node, children);
                }

                children.Next();

                node.Value = BindExpression(node, children);
            });
        }

        private IdentifierExpressionNode BindIdentifierExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<IdentifierExpressionNode>(parent, parentChildren, (node, children) =>
            {
                node.Identifier = BindIdentifier(node, parentChildren);
            });
        }

        private MemberAccessExpressionNode BindMemberAccessExpression(SyntaxNode parent, Scanner<CommonTree> parentChildren)
        {
            return CreateNode<MemberAccessExpressionNode>(parent, parentChildren, (node, children) =>
            {
                children.Next();
                node.BaseExpression = BindExpression(node, children);

                children.Next();
                node.AccessExpression = BindExpression(node, children);
            });
        }

        private static bool ShouldContractNodeEnd(SyntaxNode node)
        {
            return node is IStatementBlock
                || node is PropertyHeaderNode
                || node is DeclareStatementNode;
        }

        #endregion

        private T CreateNode<T>(SyntaxNode parent, Scanner<CommonTree> parentChildren, Action<T, Scanner<CommonTree>> bindAction = null)
            where T : SyntaxNode
        {
            var compilerNode = parentChildren.Current;
            var range = compilerNode.GetRange(_tokenStream, _text);

            if (compilerNode is CommonErrorNode errorNode)
            {
                _diagnostics.Add(errorNode.trappedException.ToDiagnostic(range));
            }

            var text = _text.GetTextInRange(range).TrimEnd();

            var node = Activator.CreateInstance<T>();
            node.Parent = parent;
            node.Script = _scriptNode;
            node.CompilerNode = compilerNode;

            node.Text = text;
            node.Range = new Range()
            {
                Start = range.Start,
                End = _text.PositionAt(_text.OffsetAt(range.Start) + text.Length)
            };

            try
            {
                bindAction?.Invoke(node, new Scanner<CommonTree>(compilerNode.GetChildren()));
            }
            catch (Exception e)
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticLevel.Error, e.Message, Range.IsEmpty(node.Range) ? parent.Range : node.Range, e));
            }

            if (ShouldContractNodeEnd(node))
            {
                var lastChild = node.Children.LastOrDefault(c => c.Range != Range.Empty);

                node.Range = new Range()
                {
                    Start = node.Range.Start,
                    End = lastChild != null && lastChild.Range.End < node.Range.End ? lastChild.Range.End : node.Range.End,
                };
            }

            // Expand parent to contain this child.
            if (node.Range != Range.Empty)
            {
                parent.Range = new Range()
                {
                    Start = parent.Range.Start > node.Range.Start ? node.Range.Start : parent.Range.Start,
                    End = parent.Range.End < node.Range.End ? node.Range.End : parent.Range.End,
                };
            }

            var indexInParent = parent.Children.IndexOf(node);
            if (indexInParent != 0)
            {
                var previousPeer = parent.Children[indexInParent - 1];
                var leadingRange = new Range()
                {
                    Start = previousPeer.Range.End,
                    End = node.Range.Start
                };

                node.LeadingText = _text.GetTextInRange(leadingRange);
            }

            return node;
        }
    }
}