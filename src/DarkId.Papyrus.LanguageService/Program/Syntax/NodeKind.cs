using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Program.Syntax
{
    public enum NodeKind
    {
        ArrayIndexExpression,
        AssignmentStatement,
        BinaryOperationExpression,
        BoolLiteral,
        CastExpression,
        CustomEventDefinition,
        DeclareStatement,
        DocComment,
        EventDefinition,
        ExpressionStatement,
        FloatLiteral,
        FunctionCallExpression,
        FunctionCallExpressionParameter,
        FunctionDefinition,
        FunctionParameter,
        GroupDefinition,
        HexLiteral,
        Identifier,
        IdentifierExpression,
        IfStatement,
        IfStatementBody,
        Import,
        IntLiteral,
        IsExpression,
        LiteralExpression,
        MemberAccessExpression,
        NewArrayExpression,
        NewStructExpression,
        NoneLiteral,
        PropertyDefinition,
        ReturnStatement,
        Script,
        StateDefinition,
        StringLiteral,
        StructDefinition,
        TypeIdentifier,
        UnaryOperationExpression,
        VariableDefinition,
        WhileStatement,
        ScriptHeader,
        FunctionHeader,
        StructHeader,
        GroupHeader,
        PropertyHeader,
    }
}
