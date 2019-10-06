using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public interface ISymbolScope
    {
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
        public abstract SyntaxKind Kind { get; }

        public string Text { get; set; }

        public ScriptNode Script { get; set; }

        public TextRange Range { get; set; }

        public TextRange FullRange { get; set; }

        public PapyrusSymbol Symbol { get; set; }

        public ISymbolScope Scope { get; set; }
    }

    public class ScriptNode : SyntaxNode, IDefinitionBlock
    {
        public override SyntaxKind Kind => SyntaxKind.Script;

        public ScriptFile File { get; set; }
        public PapyrusProgram Program { get; set; }

        public ScriptHeaderNode Header { get; set; }

        public List<ImportNode> Imports { get; } = new List<ImportNode>();
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();

        public new ScriptSymbol Symbol => (ScriptSymbol)base.Symbol;

        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class ScriptHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override SyntaxKind Kind => SyntaxKind.ScriptHeader;

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
        public override SyntaxKind Kind => SyntaxKind.AssignmentStatement;

        public ExpressionNode LeftValue { get; set; }
        public AssignmentOperatorType Operation { get; set; }
        public ExpressionNode RightValue { get; set; }
    }

    public class DeclareStatementNode : SyntaxNode, ITypedIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.DeclareStatement;

        public IdentifierNode Identifier { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
        public ExpressionNode InitialValue { get; set; }
    }

    public class ExpressionStatementNode : SyntaxNode
    {
        public override SyntaxKind Kind => SyntaxKind.ExpressionStatement;

        public ExpressionNode Expression { get; set; }
    }

    public class WhileStatementNode : SyntaxNode, IStatementBlock
    {
        public override SyntaxKind Kind => SyntaxKind.WhileStatement;

        public ExpressionNode Expression { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class ArrayIndexExpressionNode : ExpressionNode
    {
        public override SyntaxKind Kind => SyntaxKind.ArrayIndexExpression;

        public ExpressionNode ArrayExpression { get; set; }
        public ExpressionNode IndexExpression { get; set; }
    }

    public class BinaryOperationExpressionNode : ExpressionNode
    {
        public override SyntaxKind Kind => SyntaxKind.BinaryOperationExpression;

        public ExpressionNode Left { get; set; }
        public BinaryOperatorType Operator { get; set; }
        public ExpressionNode Right { get; set; }
    }

    public class CastExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.CastExpression;

        public ExpressionNode InnerExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class ReturnStatementNode : SyntaxNode
    {
        public override SyntaxKind Kind => SyntaxKind.ReturnStatement;

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
        public override SyntaxKind Kind => SyntaxKind.IfStatement;

        public List<IfStatementBodyNode> Bodies { get; } = new List<IfStatementBodyNode>();
    }

    public class IfStatementBodyNode : SyntaxNode,
        IStatementBlock
    {
        public override SyntaxKind Kind => SyntaxKind.IfStatementBody;

        public IfStatementBodyKind BodyKind { get; set; }
        public ExpressionNode Condition { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();

        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class FunctionCallExpressionNode : ExpressionNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.FunctionCallExpression;

        public bool IsGlobalCall { get; set; }
        public IdentifierNode Identifier { get; set; }
        public List<FunctionCallExpressionParameterNode> Parameters { get; } = new List<FunctionCallExpressionParameterNode>();
    }

    public class FunctionCallExpressionParameterNode : ExpressionNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.FunctionCallExpressionParameter;

        public IdentifierNode Identifier { get; set; }
        public ExpressionNode Value { get; set; }
    }

    public class IdentifierExpressionNode : ExpressionNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.IdentifierExpression;

        public IdentifierNode Identifier { get; set; }
    }

    public class IsExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.IsExpression;

        public ExpressionNode InnerExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class LiteralExpressionNode : ExpressionNode
    {
        public override SyntaxKind Kind => SyntaxKind.LiteralExpression;

        public ILiteralNode Value { get; set; }
    }

    public class MemberAccessExpressionNode : ExpressionNode
    {
        public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;

        public ExpressionNode BaseExpression { get; set; }
        public ExpressionNode AccessExpression { get; set; }
    }

    public class NewArrayExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.NewArrayExpression;

        public ExpressionNode LengthExpression { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class NewStructExpressionNode : ExpressionNode, ITypeIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.NewStructExpression;

        public TypeIdentifierNode TypeIdentifier { get; set; }
    }

    public class UnaryOperationExpressionNode : ExpressionNode
    {
        public override SyntaxKind Kind => SyntaxKind.UnaryOperationExpression;

        public ExpressionNode InnerExpression { get; set; }
        public UnaryOperatorType Operator { get; set; }
    }

    public class CustomEventDefinitionNode : SyntaxNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.CustomEventDefinition;
        public IdentifierNode Identifier { get; set; }
    }

    public class IdentifierNode : SyntaxNode
    {
        public override SyntaxKind Kind => SyntaxKind.Identifier;
    }

    public class TypeIdentifierNode : IdentifierNode
    {
        public override SyntaxKind Kind => SyntaxKind.TypeIdentifier;
        public bool IsArray { get; set; }
    }

    public class DocCommentNode : SyntaxNode
    {
        public override SyntaxKind Kind => SyntaxKind.DocComment;

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
                    comment = comment[..^1];
                }

                return comment;
            }
        }
    }

    public class VariableDefinitionNode : SyntaxNode, IFlaggable, ITypedIdentifiable, IDocumentable
    {
        public override SyntaxKind Kind => SyntaxKind.VariableDefinition;

        public LanguageFlags Flags { get; set; }
        public IdentifierNode Identifier { get; set; }
        public TypeIdentifierNode TypeIdentifier { get; set; }
        public DocCommentNode DocComment { get; set; }
    }

    public class StructDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override SyntaxKind Kind => SyntaxKind.StructDefinition;

        public StructHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class StructHeaderNode : SyntaxNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.StructHeader;

        public IdentifierNode Identifier { get; set; }
    }

    public class StateDefinitionNode : SyntaxNode, IIdentifiable, IDefinitionBlock
    {
        public override SyntaxKind Kind => SyntaxKind.StateDefinition;

        public IdentifierNode Identifier { get; set; }
        public bool IsAuto { get; set; }

        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class EventDefinitionNode : SyntaxNode,
        IStatementBlock
    {
        public override SyntaxKind Kind => SyntaxKind.EventDefinition;

        public FunctionHeaderNode Header { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class FunctionDefinitionNode : SyntaxNode,
        IStatementBlock
    {
        public override SyntaxKind Kind => SyntaxKind.FunctionDefinition;

        public FunctionHeaderNode Header { get; set; }
        public List<SyntaxNode> Statements { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class FunctionHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override SyntaxKind Kind => SyntaxKind.FunctionHeader;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }

        public MemberAccessExpressionNode RemoteEventExpression { get; set; }

        public List<FunctionParameterNode> Parameters { get; } = new List<FunctionParameterNode>();
        public LanguageFlags Flags { get; set; }
        public DocCommentNode DocComment { get; set; }
        public bool IsEvent { get; set; }
    }

    public class FunctionParameterNode : SyntaxNode,
        ITypedIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.FunctionParameter;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }
        public ILiteralNode DefaultValue { get; set; }

        public bool IsOptional { get; set; }
    }

    public class PropertyDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override SyntaxKind Kind => SyntaxKind.PropertyDefinition;

        public PropertyHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class PropertyHeaderNode : SyntaxNode, ITypedIdentifiable, IFlaggable, IDocumentable
    {
        public override SyntaxKind Kind => SyntaxKind.PropertyHeader;

        public TypeIdentifierNode TypeIdentifier { get; set; }
        public IdentifierNode Identifier { get; set; }
        public LanguageFlags Flags { get; set; }
        public DocCommentNode DocComment { get; set; }
    }

    public class GroupDefinitionNode : SyntaxNode, IDefinitionBlock
    {
        public override SyntaxKind Kind => SyntaxKind.GroupDefinition;

        public GroupHeaderNode Header { get; set; }
        public List<SyntaxNode> Definitions { get; } = new List<SyntaxNode>();
        public Dictionary<string, PapyrusSymbol> ScopedSymbols { get; } = new Dictionary<string, PapyrusSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public class GroupHeaderNode : SyntaxNode, IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.GroupHeader;
        public IdentifierNode Identifier { get; set; }
    }

    public class ImportNode : SyntaxNode,
        IIdentifiable
    {
        public override SyntaxKind Kind => SyntaxKind.Import;

        public IdentifierNode Identifier { get; set; }
    }

    public interface ILiteralNode
    {
    }

    public interface ILiteralNode<out T> : ILiteralNode
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
        public override SyntaxKind Kind => SyntaxKind.FloatLiteral;
    }

    public class BoolLiteralNode : LiteralNode<bool>
    {
        public override SyntaxKind Kind => SyntaxKind.BoolLiteral;
    }

    public class HexLiteralNode : LiteralNode<int>
    {
        public override SyntaxKind Kind => SyntaxKind.HexLiteral;
    }

    public class IntLiteralNode : LiteralNode<int>
    {
        public override SyntaxKind Kind => SyntaxKind.IntLiteral;
    }

    public class NoneLiteralNode : SyntaxNode,
        ILiteralNode
    {
        public override SyntaxKind Kind => SyntaxKind.NoneLiteral;
    }

    public class StringLiteralNode : LiteralNode<string>
    {
        public override SyntaxKind Kind => SyntaxKind.StringLiteral;
    }
}
