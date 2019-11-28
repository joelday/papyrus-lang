using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax.InternalSyntax
{
    internal interface IGreenNodeVisitor
    {
        void Visit(ArrayIndexExpressionSyntax node);
        void Visit(AssignmentStatementSyntax node);
        void Visit(BinaryOperationExpressionSyntax node);
        void Visit(CastExpressionSyntax node);
        void Visit(CustomEventDefinitionSyntax node);
        void Visit(DeclareStatementSyntax node);
        void Visit(EventDefinitionSyntax node);
        void Visit(ExpressionStatementSyntax node);
        void Visit(FunctionCallExpressionParameterSyntax node);
        void Visit(FunctionCallExpressionSyntax node);
        void Visit(FunctionDefinitionSyntax node);
        void Visit(FunctionParameterSyntax node);
        void Visit(GroupDefinitionSyntax node);
        void Visit(GroupHeaderSyntax node);
        void Visit(IdentifierExpressionSyntax node);
        void Visit(IdentifierSyntax node);
        void Visit(IfStatementBodySyntax node);
        void Visit(IfStatementSyntax node);
        void Visit(ImportSyntax node);
        void Visit(IsExpressionSyntax node);
        void Visit(LiteralExpressionSyntax node);
        void Visit(MemberAccessExpressionSyntax node);
        void Visit(NewArrayExpressionSyntax node);
        void Visit(NewStructExpressionSyntax node);
        void Visit(ParenExpressionSyntax node);
        void Visit(PropertyDefinitionSyntax node);
        void Visit(ReturnStatementSyntax node);
        void Visit(ScriptSyntax node);
        void Visit(StateDefinitionSyntax node);
        void Visit(StructDefinitionSyntax node);
        void Visit(SyntaxToken node);
        void Visit(TypeIdentifierSyntax node);
        void Visit(UnaryOperationExpressionSyntax node);
        void Visit(VariableDefinitionSyntax node);
        void Visit(WhileStatementSyntax node);
        void Visit(ScriptHeaderSyntax node);
        void Visit(FunctionHeaderSyntax node);
        void Visit(StateHeaderSyntax node);
    }

    internal interface IGreenNodeVisitor<out T>
    {
        T Visit(ArrayIndexExpressionSyntax node);
        T Visit(AssignmentStatementSyntax node);
        T Visit(BinaryOperationExpressionSyntax node);
        T Visit(CastExpressionSyntax node);
        T Visit(CustomEventDefinitionSyntax node);
        T Visit(DeclareStatementSyntax node);
        T Visit(EventDefinitionSyntax node);
        T Visit(ExpressionStatementSyntax node);
        T Visit(FunctionCallExpressionParameterSyntax node);
        T Visit(FunctionCallExpressionSyntax node);
        T Visit(FunctionDefinitionSyntax node);
        T Visit(FunctionParameterSyntax node);
        T Visit(GroupDefinitionSyntax node);
        T Visit(GroupHeaderSyntax node);
        T Visit(IdentifierExpressionSyntax node);
        T Visit(IdentifierSyntax node);
        T Visit(IfStatementBodySyntax node);
        T Visit(IfStatementSyntax node);
        T Visit(ImportSyntax node);
        T Visit(IsExpressionSyntax node);
        T Visit(LiteralExpressionSyntax node);
        T Visit(MemberAccessExpressionSyntax node);
        T Visit(NewArrayExpressionSyntax node);
        T Visit(NewStructExpressionSyntax node);
        T Visit(ParenExpressionSyntax node);
        T Visit(PropertyDefinitionSyntax node);
        T Visit(ReturnStatementSyntax node);
        T Visit(ScriptSyntax node);
        T Visit(StateDefinitionSyntax node);
        T Visit(StructDefinitionSyntax node);
        T Visit(SyntaxToken node);
        T Visit(TypeIdentifierSyntax node);
        T Visit(UnaryOperationExpressionSyntax node);
        T Visit(VariableDefinitionSyntax node);
        T Visit(WhileStatementSyntax node);
        T Visit(ScriptHeaderSyntax node);
        T Visit(FunctionHeaderSyntax node);
        T Visit(StateHeaderSyntax node);
    }
}
