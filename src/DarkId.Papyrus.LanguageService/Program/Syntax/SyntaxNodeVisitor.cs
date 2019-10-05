using System;
using System.Collections.Generic;

namespace DarkId.Papyrus.LanguageService.Program.Syntax
{
    public abstract class SyntaxNodeVisitor<T, T1>
    {
        public virtual T Visit(SyntaxNode node, T1 t1)
        {
            switch (node.Kind)
            {
                case NodeKind.ArrayIndexExpression: return VisitArrayIndexExpression((ArrayIndexExpressionNode)node, t1);
                case NodeKind.AssignmentStatement: return VisitAssignmentStatement((AssignmentStatementNode)node, t1);
                case NodeKind.BinaryOperationExpression: return VisitBinaryOperationExpression((BinaryOperationExpressionNode)node, t1);
                case NodeKind.BoolLiteral: return VisitBoolLiteral((BoolLiteralNode)node, t1);
                case NodeKind.CastExpression: return VisitCastExpression((CastExpressionNode)node, t1);
                case NodeKind.CustomEventDefinition: return VisitCustomEventDefinition((CustomEventDefinitionNode)node, t1);
                case NodeKind.DeclareStatement: return VisitDeclareStatement((DeclareStatementNode)node, t1);
                case NodeKind.DocComment: return VisitDocComment((DocCommentNode)node, t1);
                case NodeKind.EventDefinition: return VisitEventDefinition((EventDefinitionNode)node, t1);
                case NodeKind.ExpressionStatement: return VisitExpressionStatement((ExpressionStatementNode)node, t1);
                case NodeKind.FloatLiteral: return VisitFloatLiteral((FloatLiteralNode)node, t1);
                case NodeKind.FunctionCallExpression: return VisitFunctionCallExpression((FunctionCallExpressionNode)node, t1);
                case NodeKind.FunctionCallExpressionParameter: return VisitFunctionCallExpressionParameter((FunctionCallExpressionParameterNode)node, t1);
                case NodeKind.FunctionDefinition: return VisitFunctionDefinition((FunctionDefinitionNode)node, t1);
                case NodeKind.FunctionHeader: return VisitFunctionHeader((FunctionHeaderNode)node, t1);
                case NodeKind.FunctionParameter: return VisitFunctionParameter((FunctionParameterNode)node, t1);
                case NodeKind.GroupDefinition: return VisitGroupDefinition((GroupDefinitionNode)node, t1);
                case NodeKind.GroupHeader: return VisitGroupHeader((GroupHeaderNode)node, t1);
                case NodeKind.HexLiteral: return VisitHexLiteral((HexLiteralNode)node, t1);
                case NodeKind.Identifier: return VisitIdentifier((IdentifierNode)node, t1);
                case NodeKind.IdentifierExpression: return VisitIdentifierExpression((IdentifierExpressionNode)node, t1);
                case NodeKind.IfStatement: return VisitIfStatement((IfStatementNode)node, t1);
                case NodeKind.IfStatementBody: return VisitIfStatementBody((IfStatementBodyNode)node, t1);
                case NodeKind.Import: return VisitImport((ImportNode)node, t1);
                case NodeKind.IntLiteral: return VisitIntLiteral((IntLiteralNode)node, t1);
                case NodeKind.IsExpression: return VisitIsExpression((IsExpressionNode)node, t1);
                case NodeKind.LiteralExpression: return VisitLiteralExpression((LiteralExpressionNode)node, t1);
                case NodeKind.MemberAccessExpression: return VisitMemberAccessExpression((MemberAccessExpressionNode)node, t1);
                case NodeKind.NewArrayExpression: return VisitNewArrayExpression((NewArrayExpressionNode)node, t1);
                case NodeKind.NewStructExpression: return VisitNewStructExpression((NewStructExpressionNode)node, t1);
                case NodeKind.NoneLiteral: return VisitNoneLiteral((NoneLiteralNode)node, t1);
                case NodeKind.PropertyDefinition: return VisitPropertyDefinition((PropertyDefinitionNode)node, t1);
                case NodeKind.PropertyHeader: return VisitPropertyHeader((PropertyHeaderNode)node, t1);
                case NodeKind.ReturnStatement: return VisitReturnStatement((ReturnStatementNode)node, t1);
                case NodeKind.Script: return VisitScript((ScriptNode)node, t1);
                case NodeKind.ScriptHeader: return VisitScriptHeader((ScriptHeaderNode)node, t1);
                case NodeKind.StateDefinition: return VisitStateDefinition((StateDefinitionNode)node, t1);
                case NodeKind.StringLiteral: return VisitStringLiteral((StringLiteralNode)node, t1);
                case NodeKind.StructDefinition: return VisitStructDefinition((StructDefinitionNode)node, t1);
                case NodeKind.StructHeader: return VisitStructHeader((StructHeaderNode)node, t1);
                case NodeKind.TypeIdentifier: return VisitTypeIdentifier((TypeIdentifierNode)node, t1);
                case NodeKind.UnaryOperationExpression: return VisitUnaryOperationExpression((UnaryOperationExpressionNode)node, t1);
                case NodeKind.VariableDefinition: return VisitVariableDefinition((VariableDefinitionNode)node, t1);
                case NodeKind.WhileStatement: return VisitWhileStatement((WhileStatementNode)node, t1);
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
                case NodeKind.ArrayIndexExpression: return VisitArrayIndexExpression((ArrayIndexExpressionNode)node);
                case NodeKind.AssignmentStatement: return VisitAssignmentStatement((AssignmentStatementNode)node);
                case NodeKind.BinaryOperationExpression: return VisitBinaryOperationExpression((BinaryOperationExpressionNode)node);
                case NodeKind.BoolLiteral: return VisitBoolLiteral((BoolLiteralNode)node);
                case NodeKind.CastExpression: return VisitCastExpression((CastExpressionNode)node);
                case NodeKind.CustomEventDefinition: return VisitCustomEventDefinition((CustomEventDefinitionNode)node);
                case NodeKind.DeclareStatement: return VisitDeclareStatement((DeclareStatementNode)node);
                case NodeKind.DocComment: return VisitDocComment((DocCommentNode)node);
                case NodeKind.EventDefinition: return VisitEventDefinition((EventDefinitionNode)node);
                case NodeKind.ExpressionStatement: return VisitExpressionStatement((ExpressionStatementNode)node);
                case NodeKind.FloatLiteral: return VisitFloatLiteral((FloatLiteralNode)node);
                case NodeKind.FunctionCallExpression: return VisitFunctionCallExpression((FunctionCallExpressionNode)node);
                case NodeKind.FunctionCallExpressionParameter: return VisitFunctionCallExpressionParameter((FunctionCallExpressionParameterNode)node);
                case NodeKind.FunctionDefinition: return VisitFunctionDefinition((FunctionDefinitionNode)node);
                case NodeKind.FunctionHeader: return VisitFunctionHeader((FunctionHeaderNode)node);
                case NodeKind.FunctionParameter: return VisitFunctionParameter((FunctionParameterNode)node);
                case NodeKind.GroupDefinition: return VisitGroupDefinition((GroupDefinitionNode)node);
                case NodeKind.GroupHeader: return VisitGroupHeader((GroupHeaderNode)node);
                case NodeKind.HexLiteral: return VisitHexLiteral((HexLiteralNode)node);
                case NodeKind.Identifier: return VisitIdentifier((IdentifierNode)node);
                case NodeKind.IdentifierExpression: return VisitIdentifierExpression((IdentifierExpressionNode)node);
                case NodeKind.IfStatement: return VisitIfStatement((IfStatementNode)node);
                case NodeKind.IfStatementBody: return VisitIfStatementBody((IfStatementBodyNode)node);
                case NodeKind.Import: return VisitImport((ImportNode)node);
                case NodeKind.IntLiteral: return VisitIntLiteral((IntLiteralNode)node);
                case NodeKind.IsExpression: return VisitIsExpression((IsExpressionNode)node);
                case NodeKind.LiteralExpression: return VisitLiteralExpression((LiteralExpressionNode)node);
                case NodeKind.MemberAccessExpression: return VisitMemberAccessExpression((MemberAccessExpressionNode)node);
                case NodeKind.NewArrayExpression: return VisitNewArrayExpression((NewArrayExpressionNode)node);
                case NodeKind.NewStructExpression: return VisitNewStructExpression((NewStructExpressionNode)node);
                case NodeKind.NoneLiteral: return VisitNoneLiteral((NoneLiteralNode)node);
                case NodeKind.PropertyDefinition: return VisitPropertyDefinition((PropertyDefinitionNode)node);
                case NodeKind.PropertyHeader: return VisitPropertyHeader((PropertyHeaderNode)node);
                case NodeKind.ReturnStatement: return VisitReturnStatement((ReturnStatementNode)node);
                case NodeKind.Script: return VisitScript((ScriptNode)node);
                case NodeKind.ScriptHeader: return VisitScriptHeader((ScriptHeaderNode)node);
                case NodeKind.StateDefinition: return VisitStateDefinition((StateDefinitionNode)node);
                case NodeKind.StringLiteral: return VisitStringLiteral((StringLiteralNode)node);
                case NodeKind.StructDefinition: return VisitStructDefinition((StructDefinitionNode)node);
                case NodeKind.StructHeader: return VisitStructHeader((StructHeaderNode)node);
                case NodeKind.TypeIdentifier: return VisitTypeIdentifier((TypeIdentifierNode)node);
                case NodeKind.UnaryOperationExpression: return VisitUnaryOperationExpression((UnaryOperationExpressionNode)node);
                case NodeKind.VariableDefinition: return VisitVariableDefinition((VariableDefinitionNode)node);
                case NodeKind.WhileStatement: return VisitWhileStatement((WhileStatementNode)node);
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