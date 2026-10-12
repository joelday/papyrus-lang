using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Binding;

internal sealed class BoundBlock : BoundStatement
{
    public BoundBlock(ImmutableArray<BoundStatement> statements)
    {
        Statements = statements.IsDefault ? ImmutableArray<BoundStatement>.Empty : statements;
    }

    public ImmutableArray<BoundStatement> Statements { get; }

    public override BoundKind Kind => BoundKind.Block;
}

internal sealed class BoundIf : BoundStatement
{
    public BoundIf(BoundExpression condition, BoundBlock then, BoundStatement? @else)
    {
        Condition = condition;
        Then = then;
        Else = @else;
    }

    public BoundExpression Condition { get; }

    public BoundBlock Then { get; }

    public BoundStatement? Else { get; }

    public override BoundKind Kind => BoundKind.If;
}

internal sealed class BoundWhile : BoundStatement
{
    public BoundWhile(BoundExpression condition, BoundBlock body)
    {
        Condition = condition;
        Body = body;
    }

    public BoundExpression Condition { get; }

    public BoundBlock Body { get; }

    public override BoundKind Kind => BoundKind.While;
}

internal sealed class BoundReturn : BoundStatement
{
    public BoundReturn(BoundExpression? expression)
    {
        Expression = expression;
    }

    public BoundExpression? Expression { get; }

    public override BoundKind Kind => BoundKind.Return;
}

internal sealed class BoundExpressionStatement : BoundStatement
{
    public BoundExpressionStatement(BoundExpression expression)
    {
        Expression = expression;
    }

    public BoundExpression Expression { get; }

    public override BoundKind Kind => BoundKind.ExpressionStatement;
}

internal sealed class BoundLocalDeclaration : BoundStatement
{
    public BoundLocalDeclaration(IVariableSymbol local, BoundExpression? initializer)
    {
        Local = local;
        Initializer = initializer;
    }

    public IVariableSymbol Local { get; }

    public BoundExpression? Initializer { get; }

    public override BoundKind Kind => BoundKind.LocalDeclaration;
}

internal sealed class BoundAssignment : BoundStatement
{
    public BoundAssignment(BoundExpression left, SyntaxKind operatorKind, BoundExpression right)
    {
        Left = left;
        OperatorKind = operatorKind;
        Right = right;
    }

    public BoundExpression Left { get; }

    public SyntaxKind OperatorKind { get; }

    public BoundExpression Right { get; }

    public override BoundKind Kind => BoundKind.Assignment;
}

internal sealed class BoundFunction : BoundNode
{
    public BoundFunction(IFunctionSymbol function, BoundBlock body)
        : base(function.ReturnType)
    {
        Function = function;
        Body = body;
    }

    public IFunctionSymbol Function { get; }

    public BoundBlock Body { get; }

    public override BoundKind Kind => BoundKind.Function;
}

internal sealed class BoundScript : BoundNode
{
    public BoundScript(IScriptSymbol script, ImmutableArray<BoundFunction> functions)
        : base(script)
    {
        Script = script;
        Functions = functions.IsDefault ? ImmutableArray<BoundFunction>.Empty : functions;
    }

    public IScriptSymbol Script { get; }

    public ImmutableArray<BoundFunction> Functions { get; }

    public override BoundKind Kind => BoundKind.Script;
}
