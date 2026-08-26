using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Binding;

namespace Papyrus.CodeAnalysis.Emit;

internal enum ControlFlowTerminatorKind
{
    FallThrough,
    Jump,
    Conditional,
    Return,
}

internal sealed class ControlFlowTerminator
{
    private ControlFlowTerminator(
        ControlFlowTerminatorKind kind,
        BoundExpression? condition,
        BoundExpression? expression,
        BasicBlock? destination,
        BasicBlock? trueSuccessor,
        BasicBlock? falseSuccessor)
    {
        Kind = kind;
        Condition = condition;
        Expression = expression;
        Destination = destination;
        TrueSuccessor = trueSuccessor;
        FalseSuccessor = falseSuccessor;
        Successors = CreateSuccessors(kind, destination, trueSuccessor, falseSuccessor);
    }

    public ControlFlowTerminatorKind Kind { get; }

    public BoundExpression? Condition { get; }

    public BoundExpression? Expression { get; }

    public BasicBlock? Destination { get; }

    public BasicBlock? TrueSuccessor { get; }

    public BasicBlock? FalseSuccessor { get; }

    public ImmutableArray<BasicBlock> Successors { get; }

    public static ControlFlowTerminator FallThrough()
    {
        return new ControlFlowTerminator(
            ControlFlowTerminatorKind.FallThrough,
            condition: null,
            expression: null,
            destination: null,
            trueSuccessor: null,
            falseSuccessor: null);
    }

    public static ControlFlowTerminator Jump(BasicBlock destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new ControlFlowTerminator(
            ControlFlowTerminatorKind.Jump,
            condition: null,
            expression: null,
            destination,
            trueSuccessor: null,
            falseSuccessor: null);
    }

    public static ControlFlowTerminator Conditional(
        BoundExpression condition,
        BasicBlock trueSuccessor,
        BasicBlock falseSuccessor)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(trueSuccessor);
        ArgumentNullException.ThrowIfNull(falseSuccessor);
        return new ControlFlowTerminator(
            ControlFlowTerminatorKind.Conditional,
            condition,
            expression: null,
            destination: null,
            trueSuccessor,
            falseSuccessor);
    }

    public static ControlFlowTerminator Return(BoundExpression? expression)
    {
        return new ControlFlowTerminator(
            ControlFlowTerminatorKind.Return,
            condition: null,
            expression,
            destination: null,
            trueSuccessor: null,
            falseSuccessor: null);
    }

    private static ImmutableArray<BasicBlock> CreateSuccessors(
        ControlFlowTerminatorKind kind,
        BasicBlock? destination,
        BasicBlock? trueSuccessor,
        BasicBlock? falseSuccessor)
    {
        switch (kind)
        {
            case ControlFlowTerminatorKind.Jump:
                return ImmutableArray.Create(destination!);
            case ControlFlowTerminatorKind.Conditional:
                return ImmutableArray.Create(trueSuccessor!, falseSuccessor!);
            default:
                return ImmutableArray<BasicBlock>.Empty;
        }
    }
}

internal sealed class BasicBlock
{
    private readonly List<BoundStatement> _statements = new();
    private ImmutableArray<BoundStatement> _sealedStatements;
    private ControlFlowTerminator? _terminator;

    internal BasicBlock(int ordinal)
    {
        Ordinal = ordinal;
    }

    public int Ordinal { get; }

    public ImmutableArray<BoundStatement> Statements
    {
        get
        {
            EnsureSealed();
            return _sealedStatements;
        }
    }

    public ControlFlowTerminator Terminator
    {
        get
        {
            EnsureSealed();
            return _terminator!;
        }
    }

    public ImmutableArray<BasicBlock> Successors => Terminator.Successors;

    internal bool IsSealed => _terminator is not null;

    internal bool IsEmpty => _statements.Count == 0;

    internal void Add(BoundStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        if (_terminator is not null)
        {
            throw new InvalidOperationException("Cannot add a statement to a sealed block.");
        }

        _statements.Add(statement);
    }

    internal void Seal(ControlFlowTerminator terminator)
    {
        ArgumentNullException.ThrowIfNull(terminator);
        if (_terminator is not null)
        {
            throw new InvalidOperationException("Basic block is already sealed.");
        }

        _sealedStatements = _statements.ToImmutableArray();
        _terminator = terminator;
    }

    private void EnsureSealed()
    {
        if (_terminator is null)
        {
            throw new InvalidOperationException("Basic block is not sealed.");
        }
    }
}
