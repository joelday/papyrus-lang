using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public abstract class SyntaxNodeVisitor<T, T1>
    {
        public virtual T Visit(SyntaxNode node, T1 t1)
        {
            switch (node.Kind)
            {
                case SyntaxKind.ArrayIndexExpression: return VisitArrayIndexExpression((ArrayIndexExpressionNode)node, t1);
                case SyntaxKind.AssignmentStatement: return VisitAssignmentStatement((AssignmentStatementNode)node, t1);
                case SyntaxKind.BinaryOperationExpression: return VisitBinaryOperationExpression((BinaryOperationExpressionNode)node, t1);
                case SyntaxKind.BoolLiteral: return VisitBoolLiteral((BoolLiteralNode)node, t1);
                case SyntaxKind.CastExpression: return VisitCastExpression((CastExpressionNode)node, t1);
                case SyntaxKind.CustomEventDefinition: return VisitCustomEventDefinition((CustomEventDefinitionNode)node, t1);
                case SyntaxKind.DeclareStatement: return VisitDeclareStatement((DeclareStatementNode)node, t1);
                case SyntaxKind.DocComment: return VisitDocComment((DocCommentNode)node, t1);
                case SyntaxKind.EventDefinition: return VisitEventDefinition((EventDefinitionNode)node, t1);
                case SyntaxKind.ExpressionStatement: return VisitExpressionStatement((ExpressionStatementNode)node, t1);
                case SyntaxKind.FloatLiteral: return VisitFloatLiteral((FloatLiteralNode)node, t1);
                case SyntaxKind.FunctionCallExpression: return VisitFunctionCallExpression((FunctionCallExpressionNode)node, t1);
                case SyntaxKind.FunctionCallExpressionParameter: return VisitFunctionCallExpressionParameter((FunctionCallExpressionParameterNode)node, t1);
                case SyntaxKind.FunctionDefinition: return VisitFunctionDefinition((FunctionDefinitionNode)node, t1);
                case SyntaxKind.FunctionHeader: return VisitFunctionHeader((FunctionHeaderNode)node, t1);
                case SyntaxKind.FunctionParameter: return VisitFunctionParameter((FunctionParameterNode)node, t1);
                case SyntaxKind.GroupDefinition: return VisitGroupDefinition((GroupDefinitionNode)node, t1);
                case SyntaxKind.GroupHeader: return VisitGroupHeader((GroupHeaderNode)node, t1);
                case SyntaxKind.HexLiteral: return VisitHexLiteral((HexLiteralNode)node, t1);
                case SyntaxKind.Identifier: return VisitIdentifier((IdentifierNode)node, t1);
                case SyntaxKind.IdentifierExpression: return VisitIdentifierExpression((IdentifierExpressionNode)node, t1);
                case SyntaxKind.IfStatement: return VisitIfStatement((IfStatementNode)node, t1);
                case SyntaxKind.IfStatementBody: return VisitIfStatementBody((IfStatementBodyNode)node, t1);
                case SyntaxKind.Import: return VisitImport((ImportNode)node, t1);
                case SyntaxKind.IntLiteral: return VisitIntLiteral((IntLiteralNode)node, t1);
                case SyntaxKind.IsExpression: return VisitIsExpression((IsExpressionNode)node, t1);
                case SyntaxKind.LiteralExpression: return VisitLiteralExpression((LiteralExpressionNode)node, t1);
                case SyntaxKind.MemberAccessExpression: return VisitMemberAccessExpression((MemberAccessExpressionNode)node, t1);
                case SyntaxKind.NewArrayExpression: return VisitNewArrayExpression((NewArrayExpressionNode)node, t1);
                case SyntaxKind.NewStructExpression: return VisitNewStructExpression((NewStructExpressionNode)node, t1);
                case SyntaxKind.NoneLiteral: return VisitNoneLiteral((NoneLiteralNode)node, t1);
                case SyntaxKind.PropertyDefinition: return VisitPropertyDefinition((PropertyDefinitionNode)node, t1);
                case SyntaxKind.PropertyHeader: return VisitPropertyHeader((PropertyHeaderNode)node, t1);
                case SyntaxKind.ReturnStatement: return VisitReturnStatement((ReturnStatementNode)node, t1);
                case SyntaxKind.Script: return VisitScript((ScriptNode)node, t1);
                case SyntaxKind.ScriptHeader: return VisitScriptHeader((ScriptHeaderNode)node, t1);
                case SyntaxKind.StateDefinition: return VisitStateDefinition((StateDefinitionNode)node, t1);
                case SyntaxKind.StringLiteral: return VisitStringLiteral((StringLiteralNode)node, t1);
                case SyntaxKind.StructDefinition: return VisitStructDefinition((StructDefinitionNode)node, t1);
                case SyntaxKind.StructHeader: return VisitStructHeader((StructHeaderNode)node, t1);
                case SyntaxKind.TypeIdentifier: return VisitTypeIdentifier((TypeIdentifierNode)node, t1);
                case SyntaxKind.UnaryOperationExpression: return VisitUnaryOperationExpression((UnaryOperationExpressionNode)node, t1);
                case SyntaxKind.VariableDefinition: return VisitVariableDefinition((VariableDefinitionNode)node, t1);
                case SyntaxKind.WhileStatement: return VisitWhileStatement((WhileStatementNode)node, t1);
            }

            return VisitDefault(node, t1);
        }

        public virtual T VisitDefault(SyntaxNode node, T1 t1) { return default(T); }

        public virtual T VisitArrayIndexExpression(ArrayIndexExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitAssignmentStatement(AssignmentStatementNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitBinaryOperationExpression(BinaryOperationExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitBoolLiteral(BoolLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitCastExpression(CastExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitCustomEventDefinition(CustomEventDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitDeclareStatement(DeclareStatementNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitDocComment(DocCommentNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitEventDefinition(EventDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitExpressionStatement(ExpressionStatementNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFloatLiteral(FloatLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFunctionCallExpression(FunctionCallExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFunctionCallExpressionParameter(FunctionCallExpressionParameterNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFunctionDefinition(FunctionDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFunctionHeader(FunctionHeaderNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitFunctionParameter(FunctionParameterNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitGroupDefinition(GroupDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitGroupHeader(GroupHeaderNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitHexLiteral(HexLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIdentifier(IdentifierNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIdentifierExpression(IdentifierExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIfStatement(IfStatementNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIfStatementBody(IfStatementBodyNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitImport(ImportNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIntLiteral(IntLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitIsExpression(IsExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitLiteralExpression(LiteralExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitMemberAccessExpression(MemberAccessExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitNewArrayExpression(NewArrayExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitNewStructExpression(NewStructExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitNoneLiteral(NoneLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitPropertyDefinition(PropertyDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitPropertyHeader(PropertyHeaderNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitReturnStatement(ReturnStatementNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitScript(ScriptNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitScriptHeader(ScriptHeaderNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitStateDefinition(StateDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitStringLiteral(StringLiteralNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitStructDefinition(StructDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitStructHeader(StructHeaderNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitTypeIdentifier(TypeIdentifierNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitUnaryOperationExpression(UnaryOperationExpressionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitVariableDefinition(VariableDefinitionNode node, T1 t1) { return VisitDefault(node, t1); }
        public virtual T VisitWhileStatement(WhileStatementNode node, T1 t1) { return VisitDefault(node, t1); }
    }

    public abstract class SyntaxNodeVisitor<T>
    {
        public virtual T Visit(SyntaxNode node)
        {
            switch (node.Kind)
            {
                case SyntaxKind.ArrayIndexExpression: return VisitArrayIndexExpression((ArrayIndexExpressionNode)node);
                case SyntaxKind.AssignmentStatement: return VisitAssignmentStatement((AssignmentStatementNode)node);
                case SyntaxKind.BinaryOperationExpression: return VisitBinaryOperationExpression((BinaryOperationExpressionNode)node);
                case SyntaxKind.BoolLiteral: return VisitBoolLiteral((BoolLiteralNode)node);
                case SyntaxKind.CastExpression: return VisitCastExpression((CastExpressionNode)node);
                case SyntaxKind.CustomEventDefinition: return VisitCustomEventDefinition((CustomEventDefinitionNode)node);
                case SyntaxKind.DeclareStatement: return VisitDeclareStatement((DeclareStatementNode)node);
                case SyntaxKind.DocComment: return VisitDocComment((DocCommentNode)node);
                case SyntaxKind.EventDefinition: return VisitEventDefinition((EventDefinitionNode)node);
                case SyntaxKind.ExpressionStatement: return VisitExpressionStatement((ExpressionStatementNode)node);
                case SyntaxKind.FloatLiteral: return VisitFloatLiteral((FloatLiteralNode)node);
                case SyntaxKind.FunctionCallExpression: return VisitFunctionCallExpression((FunctionCallExpressionNode)node);
                case SyntaxKind.FunctionCallExpressionParameter: return VisitFunctionCallExpressionParameter((FunctionCallExpressionParameterNode)node);
                case SyntaxKind.FunctionDefinition: return VisitFunctionDefinition((FunctionDefinitionNode)node);
                case SyntaxKind.FunctionHeader: return VisitFunctionHeader((FunctionHeaderNode)node);
                case SyntaxKind.FunctionParameter: return VisitFunctionParameter((FunctionParameterNode)node);
                case SyntaxKind.GroupDefinition: return VisitGroupDefinition((GroupDefinitionNode)node);
                case SyntaxKind.GroupHeader: return VisitGroupHeader((GroupHeaderNode)node);
                case SyntaxKind.HexLiteral: return VisitHexLiteral((HexLiteralNode)node);
                case SyntaxKind.Identifier: return VisitIdentifier((IdentifierNode)node);
                case SyntaxKind.IdentifierExpression: return VisitIdentifierExpression((IdentifierExpressionNode)node);
                case SyntaxKind.IfStatement: return VisitIfStatement((IfStatementNode)node);
                case SyntaxKind.IfStatementBody: return VisitIfStatementBody((IfStatementBodyNode)node);
                case SyntaxKind.Import: return VisitImport((ImportNode)node);
                case SyntaxKind.IntLiteral: return VisitIntLiteral((IntLiteralNode)node);
                case SyntaxKind.IsExpression: return VisitIsExpression((IsExpressionNode)node);
                case SyntaxKind.LiteralExpression: return VisitLiteralExpression((LiteralExpressionNode)node);
                case SyntaxKind.MemberAccessExpression: return VisitMemberAccessExpression((MemberAccessExpressionNode)node);
                case SyntaxKind.NewArrayExpression: return VisitNewArrayExpression((NewArrayExpressionNode)node);
                case SyntaxKind.NewStructExpression: return VisitNewStructExpression((NewStructExpressionNode)node);
                case SyntaxKind.NoneLiteral: return VisitNoneLiteral((NoneLiteralNode)node);
                case SyntaxKind.PropertyDefinition: return VisitPropertyDefinition((PropertyDefinitionNode)node);
                case SyntaxKind.PropertyHeader: return VisitPropertyHeader((PropertyHeaderNode)node);
                case SyntaxKind.ReturnStatement: return VisitReturnStatement((ReturnStatementNode)node);
                case SyntaxKind.Script: return VisitScript((ScriptNode)node);
                case SyntaxKind.ScriptHeader: return VisitScriptHeader((ScriptHeaderNode)node);
                case SyntaxKind.StateDefinition: return VisitStateDefinition((StateDefinitionNode)node);
                case SyntaxKind.StringLiteral: return VisitStringLiteral((StringLiteralNode)node);
                case SyntaxKind.StructDefinition: return VisitStructDefinition((StructDefinitionNode)node);
                case SyntaxKind.StructHeader: return VisitStructHeader((StructHeaderNode)node);
                case SyntaxKind.TypeIdentifier: return VisitTypeIdentifier((TypeIdentifierNode)node);
                case SyntaxKind.UnaryOperationExpression: return VisitUnaryOperationExpression((UnaryOperationExpressionNode)node);
                case SyntaxKind.VariableDefinition: return VisitVariableDefinition((VariableDefinitionNode)node);
                case SyntaxKind.WhileStatement: return VisitWhileStatement((WhileStatementNode)node);
            }

            return VisitDefault(node);
        }

        public virtual T VisitDefault(SyntaxNode node) { return default(T); }

        public virtual T VisitArrayIndexExpression(ArrayIndexExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitAssignmentStatement(AssignmentStatementNode node) { return VisitDefault(node); }
        public virtual T VisitBinaryOperationExpression(BinaryOperationExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitBoolLiteral(BoolLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitCastExpression(CastExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitCustomEventDefinition(CustomEventDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitDeclareStatement(DeclareStatementNode node) { return VisitDefault(node); }
        public virtual T VisitDocComment(DocCommentNode node) { return VisitDefault(node); }
        public virtual T VisitEventDefinition(EventDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitExpressionStatement(ExpressionStatementNode node) { return VisitDefault(node); }
        public virtual T VisitFloatLiteral(FloatLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitFunctionCallExpression(FunctionCallExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitFunctionCallExpressionParameter(FunctionCallExpressionParameterNode node) { return VisitDefault(node); }
        public virtual T VisitFunctionDefinition(FunctionDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitFunctionHeader(FunctionHeaderNode node) { return VisitDefault(node); }
        public virtual T VisitFunctionParameter(FunctionParameterNode node) { return VisitDefault(node); }
        public virtual T VisitGroupDefinition(GroupDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitGroupHeader(GroupHeaderNode node) { return VisitDefault(node); }
        public virtual T VisitHexLiteral(HexLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitIdentifier(IdentifierNode node) { return VisitDefault(node); }
        public virtual T VisitIdentifierExpression(IdentifierExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitIfStatement(IfStatementNode node) { return VisitDefault(node); }
        public virtual T VisitIfStatementBody(IfStatementBodyNode node) { return VisitDefault(node); }
        public virtual T VisitImport(ImportNode node) { return VisitDefault(node); }
        public virtual T VisitIntLiteral(IntLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitIsExpression(IsExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitLiteralExpression(LiteralExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitMemberAccessExpression(MemberAccessExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitNewArrayExpression(NewArrayExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitNewStructExpression(NewStructExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitNoneLiteral(NoneLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitPropertyDefinition(PropertyDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitPropertyHeader(PropertyHeaderNode node) { return VisitDefault(node); }
        public virtual T VisitReturnStatement(ReturnStatementNode node) { return VisitDefault(node); }
        public virtual T VisitScript(ScriptNode node) { return VisitDefault(node); }
        public virtual T VisitScriptHeader(ScriptHeaderNode node) { return VisitDefault(node); }
        public virtual T VisitStateDefinition(StateDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitStringLiteral(StringLiteralNode node) { return VisitDefault(node); }
        public virtual T VisitStructDefinition(StructDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitStructHeader(StructHeaderNode node) { return VisitDefault(node); }
        public virtual T VisitTypeIdentifier(TypeIdentifierNode node) { return VisitDefault(node); }
        public virtual T VisitUnaryOperationExpression(UnaryOperationExpressionNode node) { return VisitDefault(node); }
        public virtual T VisitVariableDefinition(VariableDefinitionNode node) { return VisitDefault(node); }
        public virtual T VisitWhileStatement(WhileStatementNode node) { return VisitDefault(node); }
    }
}