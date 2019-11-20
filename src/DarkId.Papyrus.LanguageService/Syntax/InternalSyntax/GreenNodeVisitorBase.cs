using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal abstract class GreenNodeVisitorBase<T> : IGreenNodeVisitor<T>
    {
        public virtual T DefaultVisit(GreenNode node)
        {
            return default;
        }

        public virtual T Visit(ArrayIndexExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(AssignmentStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(BinaryOperationExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(CastExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(CustomEventDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(DeclareStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(EventDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ExpressionStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(FunctionCallExpressionParameterSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(FunctionCallExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(FunctionDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(FunctionParameterSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(GroupDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(IdentifierExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(IdentifierSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(IfStatementBodySyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(IfStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ImportSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(IsExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(LiteralExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(MemberAccessExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(NewArrayExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(NewStructExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ParenExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(PropertyDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ReturnStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ScriptSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(StateDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(StructDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(SyntaxToken node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(TypeIdentifierSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(UnaryOperationExpressionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(VariableDefinitionSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(WhileStatementSyntax node)
        {
            return DefaultVisit(node);
        }

        public virtual T Visit(ScriptHeaderSyntax node)
        {
            return DefaultVisit(node);
        }
    }

    internal abstract class GreenNodeVisitorBase : IGreenNodeVisitor
    {
        public virtual void DefaultVisit(GreenNode node)
        {

        }

        public virtual void Visit(ArrayIndexExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(AssignmentStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(BinaryOperationExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(CastExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(CustomEventDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(DeclareStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(EventDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ExpressionStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(FunctionCallExpressionParameterSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(FunctionCallExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(FunctionDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(FunctionParameterSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(GroupDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(IdentifierExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(IdentifierSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(IfStatementBodySyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(IfStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ImportSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(IsExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(LiteralExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(MemberAccessExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(NewArrayExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(NewStructExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ParenExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(PropertyDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ReturnStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ScriptSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(StateDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(StructDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(SyntaxToken node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(TypeIdentifierSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(UnaryOperationExpressionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(VariableDefinitionSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(WhileStatementSyntax node)
        {
            DefaultVisit(node);
        }

        public virtual void Visit(ScriptHeaderSyntax node)
        {
            DefaultVisit(node);
        }
    }
}
