using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Binding;

namespace Papyrus.CodeAnalysis.Emit;

internal sealed class ControlFlowGraph
{
    private ControlFlowGraph(BoundFunction function, BasicBlock entry, ImmutableArray<BasicBlock> blocks)
    {
        Function = function;
        Entry = entry;
        Blocks = blocks;
    }

    public BoundFunction Function { get; }

    public BasicBlock Entry { get; }

    public ImmutableArray<BasicBlock> Blocks { get; }

    public static ControlFlowGraph Build(BoundFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new Builder(function).Build();
    }

    private sealed class Builder
    {
        private readonly BoundFunction _function;
        private readonly List<BasicBlock> _blocks = new();

        public Builder(BoundFunction function)
        {
            _function = function;
        }

        public ControlFlowGraph Build()
        {
            var current = CreateBlock();
            BuildStatements(_function.Body.Statements, current);
            foreach (var block in _blocks)
            {
                if (!block.IsSealed)
                {
                    block.Seal(ControlFlowTerminator.FallThrough());
                }
            }

            return new ControlFlowGraph(_function, _blocks[0], _blocks.ToImmutableArray());
        }

        private BasicBlock? BuildStatements(ImmutableArray<BoundStatement> statements, BasicBlock? current)
        {
            foreach (var statement in statements)
            {
                current = BuildStatement(statement, current);
            }

            return current;
        }

        private BasicBlock? BuildStatement(BoundStatement statement, BasicBlock? current)
        {
            switch (statement)
            {
                case BoundBlock block:
                    return BuildStatements(block.Statements, current);
                case BoundIf @if:
                    return BuildIf(@if, current ?? CreateBlock());
                case BoundWhile @while:
                    return BuildWhile(@while, current ?? CreateBlock());
                case BoundReturn ret:
                    return BuildReturn(ret, current ?? CreateBlock());
                default:
                    current ??= CreateBlock();
                    current.Add(statement);
                    return current;
            }
        }

        private BasicBlock BuildIf(BoundIf @if, BasicBlock current, BasicBlock? join = null)
        {
            var thenBlock = CreateBlock();
            BasicBlock? elseBlock = null;
            if (@if.Else is not null)
            {
                elseBlock = CreateBlock();
            }

            join ??= CreateBlock();
            current.Seal(ControlFlowTerminator.Conditional(@if.Condition, thenBlock, elseBlock ?? join));
            Finish(BuildStatements(@if.Then.Statements, thenBlock), join);

            if (@if.Else is BoundIf nested)
            {
                BuildIf(nested, elseBlock!, join);
            }
            else if (@if.Else is { } elseStatement)
            {
                Finish(BuildStatement(elseStatement, elseBlock), join);
            }

            return join;
        }

        private BasicBlock BuildWhile(BoundWhile @while, BasicBlock current)
        {
            BasicBlock header;
            if (current.IsEmpty && !current.IsSealed)
            {
                header = current;
            }
            else
            {
                header = CreateBlock();
                current.Seal(ControlFlowTerminator.Jump(header));
            }

            var body = CreateBlock();
            var exit = CreateBlock();
            header.Seal(ControlFlowTerminator.Conditional(@while.Condition, body, exit));
            Finish(BuildStatements(@while.Body.Statements, body), header);
            return exit;
        }

        private static BasicBlock? BuildReturn(BoundReturn ret, BasicBlock current)
        {
            current.Seal(ControlFlowTerminator.Return(ret.Expression));
            return null;
        }

        private static void Finish(BasicBlock? block, BasicBlock destination)
        {
            if (block is null || block.IsSealed)
            {
                return;
            }

            block.Seal(ControlFlowTerminator.Jump(destination));
        }

        private BasicBlock CreateBlock()
        {
            var block = new BasicBlock(_blocks.Count);
            _blocks.Add(block);
            return block;
        }
    }
}
