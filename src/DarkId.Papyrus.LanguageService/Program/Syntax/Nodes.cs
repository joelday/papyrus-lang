using Antlr.Runtime.Tree;
using DarkId.Papyrus.LanguageService.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using PCompiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Program.Syntax
{
    public interface ISymbolScope
    {
        string LocalScopeName { get; }
        Dictionary<string, PapyrusSymbol> ScopedSymbols { get; }
    }

    public interface IFlaggable
    {
        LanguageFlags Flags { get; }
    }

    public interface IIdentifiable
    {
        IdentifierNode Identifier { get; set; }
    }

    public interface ITypeIdentifiable
    {
        TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public interface ITypedIdentifiable : IIdentifiable, ITypeIdentifiable
    {

    }

    public interface IDefinitionBlock : ISymbolScope
    {
        List<SyntaxNode> Definitions { get; }
    }

    public interface IStatementBlock : ISymbolScope
    {
        List<SyntaxNode> Statements { get; }
    }

    public interface IDocumentable
    {
        DocCommentNode DocComment { get; set; }
    }

    public abstract class SyntaxNode : TreeNode<SyntaxNode>
    {
        public abstract NodeKind Kind { get; }

        public string LeadingText { get; set; }

        public string Text { get; set; }

        public ScriptNode Script { get; set; }

        public Range Range { get; set; }

        internal CommonTree CompilerNode { get; set; }

        public PapyrusSymbol Symbol { get; set; }

        public ISymbolScope Scope { get; set; }

        internal ScriptScope CompilerScope { get; set; }
    }

    public class ScriptNode : SyntaxNode, IDefinitionBlock
    {
        public override NodeKind Kind => NodeKind.Script;

        public ScriptFile File { get; set; }
        public PapyrusProgram Program { get; set; }

        public string LocalScopeName => null;

        public ScriptHeaderNode Header { get; set; }

        public List<ImportNode> Imports { get; } = new List<ImportNode>();
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();

        internal ScriptObjectType CompilerType { get; set; }

        public new ScriptSymbol Symbol => (ScriptSymbol)base.Symbol;

        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class ScriptHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override NodeKind Kind => NodeKind.ScriptHeader;

        public IdentifierNode Identifier { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
        public LanguageFlags Flags { get; set; }
        public DocCommentNode DocComment { get; set; }
    }

    public abstract class ExpressionNode : SyntaxNode
    {
        
    }

    public class AssignmentStatementNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.AssignmentStatement;

        public ExpressionNode LeftValue { get; set; }
        public AssignmentOperatorType Operation { get; set; }
        public ExpressionNode RightValue { get; set; }
    }

    public class DeclareStatementNode : SyntaxNode, ITypedIdentifiable
    {
        public override NodeKind Kind => NodeKind.DeclareStatement;

        public IdentifierNode Identifier { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
        public ExpressionNode InitialValue { get; set; }

        internal ScriptVariableType CompilerType { get; set; }
    }

    public class ExpressionStatementNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.ExpressionStatement;

        public ExpressionNode Expression { get; set; }
    }

    public class WhileStatementNode : SyntaxNode, IStatementBlock
    {
        public override NodeKind Kind => NodeKind.WhileStatement;

        public ExpressionNode Expression { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => "while";
    }

    public class ArrayIndexExpressionNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.ArrayIndexExpression;

        public ExpressionNode ArrayExpression { get; set; }
        public ExpressionNode IndexExpression { get; set; }
    }

    public class BinaryOperationExpressionNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.BinaryOperationExpression;

        public ExpressionNode Left { get; set; }
        public BinaryOperatorType Operator { get; set; }
        public ExpressionNode Right { get; set; }
    }

    public class CastExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override NodeKind Kind => NodeKind.CastExpression;

        public ExpressionNode InnerExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class ReturnStatementNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.ReturnStatement;

        public ExpressionNode ReturnValue { get; set; }
    }

    public enum IfStatementBodyKind
    {
        If,
        ElseIf,
        Else
    }

    public class IfStatementNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.IfStatement;

        public List<IfStatementBodyNode> Bodies { get; } = new List<IfStatementBodyNode>();
    }

    public class IfStatementBodyNode : SyntaxNode,
        IStatementBlock
    {
        public override NodeKind Kind => NodeKind.IfStatementBody;

        public IfStatementBodyKind BodyKind { get; set; }
        public ExpressionNode Condition { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();

        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => BodyKind.ToString().ToLower();
    }

    public class FunctionCallExpressionNode : ExpressionNode, IIdentifiable
    {
        public override NodeKind Kind => NodeKind.FunctionCallExpression;

        public bool IsGlobalCall { get; set; }
        public IdentifierNode Identifier { get; set; }
        public List<FunctionCallExpressionParameterNode> Parameters { get; } = new List<FunctionCallExpressionParameterNode>();
    }

    public class FunctionCallExpressionParameterNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.FunctionCallExpressionParameter;

        public ExpressionNode Value { get; set; }
    }

    public class IdentifierExpressionNode : ExpressionNode, IIdentifiable
    {
        public override NodeKind Kind => NodeKind.IdentifierExpression;

        public IdentifierNode Identifier { get; set; }
    }

    public class IsExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override NodeKind Kind => NodeKind.IsExpression;

        public ExpressionNode InnerExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class LiteralExpressionNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.LiteralExpression;

        public ILiteralNode Value { get; set; }
    }

    public class MemberAccessExpressionNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.MemberAccessExpression;

        public ExpressionNode BaseExpression { get; set; }
        public ExpressionNode AccessExpression { get; set; }
    }

    public class NewArrayExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override NodeKind Kind => NodeKind.NewArrayExpression;

        public ExpressionNode LengthExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class NewStructExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override NodeKind Kind => NodeKind.NewStructExpression;

        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class UnaryOperationExpressionNode : ExpressionNode
    {
        public override NodeKind Kind => NodeKind.UnaryOperationExpression;

        public ExpressionNode InnerExpression { get; set; }
        public UnaryOperatorType Operator { get; set; }
    }

    public class CustomEventDefinitionNode : SyntaxNode, IIdentifiable
    {
        public override NodeKind Kind => NodeKind.CustomEventDefinition;
        public IdentifierNode Identifier { get; set; }
    }

    public class IdentifierNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.Identifier;
    }

    public class TypeIdentifierNode : IdentifierNode
    {
        public override NodeKind Kind => NodeKind.TypeIdentifier;
        public bool IsArray { get; set; }
    }

    public class DocCommentNode : SyntaxNode
    {
        public override NodeKind Kind => NodeKind.DocComment;

        public string Comment
        {
            get
            {
                var comment = Text;
                if (comment.FirstOrDefault() == '{')
                {
                    comment = comment.Substring(1);
                }

                if (comment.FirstOrDefault() == ' ')
                {
                    comment = comment.Substring(1);
                }

                if (comment.LastOrDefault() == '}')
                {
                    comment = comment.Substring(0, comment.Length - 1);
                }

                return comment;
            }
        }
    }

    public class VariableDefinitionNode : SyntaxNode, IFlaggable, ITypedIdentifiable, IDocumentable
    {
        public override NodeKind Kind => NodeKind.VariableDefinition;

        public LanguageFlags Flags { get; set; }
        public IdentifierNode Identifier { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
        public DocCommentNode DocComment { get; set; }

        internal ScriptVariableType CompilerType { get; set; }
    }

    public class StructDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override NodeKind Kind => NodeKind.StructDefinition;

        public StructHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => null;
    }

    public class StructHeaderNode : SyntaxNode, IIdentifiable
    {
        public override NodeKind Kind => NodeKind.StructHeader;

        internal ScriptStructType CompilerType { get; set; }

        public IdentifierNode Identifier { get; set; }
    }

    public class StateDefinitionNode : SyntaxNode, IIdentifiable, IDefinitionBlock
    {
        public override NodeKind Kind => NodeKind.StateDefinition;

        public IdentifierNode Identifier { get; set; }
        public bool IsAuto { get; set; }

        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => Identifier.Text;
    }

    public class EventDefinitionNode : SyntaxNode,
        IStatementBlock
    {
        public override NodeKind Kind => NodeKind.EventDefinition;

        public FunctionHeaderNode Header { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => Header.Identifier.Text;
    }

    public class FunctionDefinitionNode : SyntaxNode,
        IStatementBlock
    {
        public override NodeKind Kind => NodeKind.FunctionDefinition;

        public FunctionHeaderNode Header { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => Header.Identifier.Text;
    }

    public class FunctionHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override NodeKind Kind => NodeKind.FunctionHeader;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }
        public List<FunctionParameterNode> Parameters { get; } = new List<FunctionParameterNode>();
        public LanguageFlags Flags { get; set; }
        public DocCommentNode DocComment { get; set; }

        internal ScriptFunctionType CompilerType { get; set; }
    }

    public class FunctionParameterNode : SyntaxNode,
        ITypedIdentifiable
    {
        public override NodeKind Kind => NodeKind.FunctionParameter;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }
        public ILiteralNode DefaultValue { get; set; }

        public bool IsOptional { get; set; }
    }

    public class PropertyDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override NodeKind Kind => NodeKind.PropertyDefinition;

        public PropertyHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => Header.Identifier.Text;
    }

    public class PropertyHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override NodeKind Kind => NodeKind.PropertyHeader;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }
        public LanguageFlags Flags { get; set; }
        public DocCommentNode DocComment { get; set; }

        internal ScriptPropertyType CompilerType { get; set; }
    }

    public class GroupDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override NodeKind Kind => NodeKind.GroupDefinition;

        public GroupHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);

        public string LocalScopeName => Header.Identifier.Text;
    }

    public class GroupHeaderNode : SyntaxNode, IIdentifiable
    {
        public override NodeKind Kind => NodeKind.GroupHeader;
        public IdentifierNode Identifier { get; set; }
    }

    public class ImportNode : SyntaxNode,
        IIdentifiable
    {
        public override NodeKind Kind => NodeKind.Import;

        public IdentifierNode Identifier { get; set; }
    }

    public interface ILiteralNode
    {
    }

    public interface ILiteralNode<T> : ILiteralNode
    {
        T Value { get; }
    }

    public abstract class LiteralNode<T> : SyntaxNode,
        ILiteralNode<T>
    {
        public T Value { get; set; }
    }

    public class FloatLiteralNode : LiteralNode<float>
    {
        public override NodeKind Kind => NodeKind.FloatLiteral;
    }

    public class BoolLiteralNode : LiteralNode<bool>
    {
        public override NodeKind Kind => NodeKind.BoolLiteral;
    }

    public class HexLiteralNode : LiteralNode<int>
    {
        public override NodeKind Kind => NodeKind.HexLiteral;
    }

    public class IntLiteralNode : LiteralNode<int>
    {
        public override NodeKind Kind => NodeKind.IntLiteral;
    }

    public class NoneLiteralNode : SyntaxNode,
        ILiteralNode
    {
        public override NodeKind Kind => NodeKind.NoneLiteral;
    }

    public class StringLiteralNode : LiteralNode<string>
    {
        public override NodeKind Kind => NodeKind.StringLiteral;
    }
}
