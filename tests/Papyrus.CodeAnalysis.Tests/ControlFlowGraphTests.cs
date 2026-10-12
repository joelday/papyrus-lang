using System.Collections.Generic;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Text;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class ControlFlowGraphTests
{
    [Fact]
    public void Build_straight_line_is_single_fallthrough_block()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n  Int x = 1\n  x = 2\nEndFunction\n");

        var block = Assert.Single(graph.Blocks);
        Assert.Same(graph.Entry, block);
        Assert.Equal(0, block.Ordinal);
        Assert.Equal(2, block.Statements.Length);
        Assert.Same(lowered.Body.Statements[0], block.Statements[0]);
        Assert.Same(lowered.Body.Statements[1], block.Statements[1]);
        Assert.Equal(ControlFlowTerminatorKind.FallThrough, block.Terminator.Kind);
        Assert.Empty(block.Successors);
    }

    [Fact]
    public void Build_if_then_joins_to_successor()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  EndIf\n" +
            "  x = 3\n" +
            "EndFunction\n");

        var @if = Assert.IsType<BoundIf>(lowered.Body.Statements[1]);
        Assert.Equal(3, graph.Blocks.Length);

        var entry = graph.Entry;
        var thenBlock = graph.Blocks[1];
        var join = graph.Blocks[2];

        Assert.Same(lowered.Body.Statements[0], Assert.Single(entry.Statements));
        Assert.Equal(ControlFlowTerminatorKind.Conditional, entry.Terminator.Kind);
        Assert.Same(@if.Condition, entry.Terminator.Condition);
        Assert.Same(thenBlock, entry.Terminator.TrueSuccessor);
        Assert.Same(join, entry.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(@if.Then.Statements), Assert.Single(thenBlock.Statements));
        AssertJump(thenBlock, join);

        Assert.Same(lowered.Body.Statements[2], Assert.Single(join.Statements));
        Assert.Equal(ControlFlowTerminatorKind.FallThrough, join.Terminator.Kind);
        Assert.Empty(join.Successors);
    }

    [Fact]
    public void Build_if_else_both_arms_join()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  Else\n" +
            "    x = 3\n" +
            "  EndIf\n" +
            "EndFunction\n");

        var @if = Assert.IsType<BoundIf>(lowered.Body.Statements[1]);
        var elseBlockSyntax = Assert.IsType<BoundBlock>(@if.Else);
        Assert.Equal(4, graph.Blocks.Length);

        var entry = graph.Entry;
        var thenBlock = graph.Blocks[1];
        var elseBlock = graph.Blocks[2];
        var join = graph.Blocks[3];

        Assert.Equal(ControlFlowTerminatorKind.Conditional, entry.Terminator.Kind);
        Assert.Same(@if.Condition, entry.Terminator.Condition);
        Assert.Same(thenBlock, entry.Terminator.TrueSuccessor);
        Assert.Same(elseBlock, entry.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(@if.Then.Statements), Assert.Single(thenBlock.Statements));
        AssertJump(thenBlock, join);

        Assert.Same(Assert.Single(elseBlockSyntax.Statements), Assert.Single(elseBlock.Statements));
        AssertJump(elseBlock, join);

        Assert.Empty(join.Statements);
        Assert.Equal(ControlFlowTerminatorKind.FallThrough, join.Terminator.Kind);
        Assert.Empty(join.Successors);
    }

    [Fact]
    public void Build_else_if_chain_shares_join()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  ElseIf x\n" +
            "    x = 3\n" +
            "  Else\n" +
            "    x = 4\n" +
            "  EndIf\n" +
            "EndFunction\n");

        var @if = Assert.IsType<BoundIf>(lowered.Body.Statements[1]);
        var elseIf = Assert.IsType<BoundIf>(@if.Else);
        var elseBlockSyntax = Assert.IsType<BoundBlock>(elseIf.Else);
        Assert.Equal(6, graph.Blocks.Length);

        var entry = graph.Entry;
        var thenBlock = graph.Blocks[1];
        var elseIfBlock = graph.Blocks[2];
        var join = graph.Blocks[3];
        var elseIfThen = graph.Blocks[4];
        var elseBlock = graph.Blocks[5];

        Assert.Equal(ControlFlowTerminatorKind.Conditional, entry.Terminator.Kind);
        Assert.Same(@if.Condition, entry.Terminator.Condition);
        Assert.Same(thenBlock, entry.Terminator.TrueSuccessor);
        Assert.Same(elseIfBlock, entry.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(@if.Then.Statements), Assert.Single(thenBlock.Statements));
        AssertJump(thenBlock, join);

        Assert.Equal(ControlFlowTerminatorKind.Conditional, elseIfBlock.Terminator.Kind);
        Assert.Same(elseIf.Condition, elseIfBlock.Terminator.Condition);
        Assert.Same(elseIfThen, elseIfBlock.Terminator.TrueSuccessor);
        Assert.Same(elseBlock, elseIfBlock.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(elseIf.Then.Statements), Assert.Single(elseIfThen.Statements));
        AssertJump(elseIfThen, join);

        Assert.Same(Assert.Single(elseBlockSyntax.Statements), Assert.Single(elseBlock.Statements));
        AssertJump(elseBlock, join);

        Assert.Equal(ControlFlowTerminatorKind.FallThrough, join.Terminator.Kind);
        Assert.Empty(join.Successors);
    }

    [Fact]
    public void Build_while_header_loops_back_from_body()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  While x\n" +
            "    x = 0\n" +
            "  EndWhile\n" +
            "EndFunction\n");

        var @while = Assert.IsType<BoundWhile>(lowered.Body.Statements[1]);
        Assert.Equal(4, graph.Blocks.Length);

        var entry = graph.Entry;
        var header = graph.Blocks[1];
        var body = graph.Blocks[2];
        var exit = graph.Blocks[3];

        Assert.Same(lowered.Body.Statements[0], Assert.Single(entry.Statements));
        AssertJump(entry, header);

        Assert.Empty(header.Statements);
        Assert.Equal(ControlFlowTerminatorKind.Conditional, header.Terminator.Kind);
        Assert.Same(@while.Condition, header.Terminator.Condition);
        Assert.Same(body, header.Terminator.TrueSuccessor);
        Assert.Same(exit, header.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(@while.Body.Statements), Assert.Single(body.Statements));
        AssertJump(body, header);

        Assert.Empty(exit.Statements);
        Assert.Equal(ControlFlowTerminatorKind.FallThrough, exit.Terminator.Kind);
        Assert.Empty(exit.Successors);
    }

    [Fact]
    public void Build_nested_while_inner_exit_jumps_to_outer_header()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  While x\n" +
            "    While x\n" +
            "      x = 0\n" +
            "    EndWhile\n" +
            "  EndWhile\n" +
            "EndFunction\n");

        var outer = Assert.IsType<BoundWhile>(lowered.Body.Statements[1]);
        var inner = Assert.IsType<BoundWhile>(Assert.Single(outer.Body.Statements));
        Assert.Equal(6, graph.Blocks.Length);

        var entry = graph.Entry;
        var outerHeader = graph.Blocks[1];
        var innerHeader = graph.Blocks[2];
        var outerExit = graph.Blocks[3];
        var innerBody = graph.Blocks[4];
        var innerExit = graph.Blocks[5];

        AssertJump(entry, outerHeader);

        Assert.Equal(ControlFlowTerminatorKind.Conditional, outerHeader.Terminator.Kind);
        Assert.Same(outer.Condition, outerHeader.Terminator.Condition);
        Assert.Same(innerHeader, outerHeader.Terminator.TrueSuccessor);
        Assert.Same(outerExit, outerHeader.Terminator.FalseSuccessor);

        Assert.Equal(ControlFlowTerminatorKind.Conditional, innerHeader.Terminator.Kind);
        Assert.Same(inner.Condition, innerHeader.Terminator.Condition);
        Assert.Same(innerBody, innerHeader.Terminator.TrueSuccessor);
        Assert.Same(innerExit, innerHeader.Terminator.FalseSuccessor);

        Assert.Same(Assert.Single(inner.Body.Statements), Assert.Single(innerBody.Statements));
        AssertJump(innerBody, innerHeader);
        AssertJump(innerExit, outerHeader);

        Assert.Equal(ControlFlowTerminatorKind.FallThrough, outerExit.Terminator.Kind);
        Assert.Empty(outerExit.Successors);
    }

    [Fact]
    public void Build_return_in_the_middle_leaves_following_statements_unreachable()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  Return\n" +
            "  x = 2\n" +
            "EndFunction\n");

        var ret = Assert.IsType<BoundReturn>(lowered.Body.Statements[1]);
        Assert.Equal(2, graph.Blocks.Length);

        var entry = graph.Entry;
        var unreachable = graph.Blocks[1];

        Assert.Same(lowered.Body.Statements[0], Assert.Single(entry.Statements));
        Assert.Equal(ControlFlowTerminatorKind.Return, entry.Terminator.Kind);
        Assert.Null(entry.Terminator.Expression);
        Assert.Same(ret.Expression, entry.Terminator.Expression);
        Assert.Empty(entry.Successors);

        Assert.Same(lowered.Body.Statements[2], Assert.Single(unreachable.Statements));
        Assert.Equal(ControlFlowTerminatorKind.FallThrough, unreachable.Terminator.Kind);
        Assert.Empty(unreachable.Successors);
    }

    [Fact]
    public void Build_return_in_both_if_arms_keeps_join()
    {
        var (graph, lowered) = BuildGraph(
            "ScriptName Foo\nInt Function Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    Return 1\n" +
            "  Else\n" +
            "    Return 2\n" +
            "  EndIf\n" +
            "EndFunction\n");

        var @if = Assert.IsType<BoundIf>(lowered.Body.Statements[1]);
        var thenReturn = Assert.IsType<BoundReturn>(Assert.Single(@if.Then.Statements));
        var elseReturn = Assert.IsType<BoundReturn>(
            Assert.Single(Assert.IsType<BoundBlock>(@if.Else).Statements));
        Assert.Equal(4, graph.Blocks.Length);

        var entry = graph.Entry;
        var thenBlock = graph.Blocks[1];
        var elseBlock = graph.Blocks[2];
        var join = graph.Blocks[3];

        Assert.Equal(ControlFlowTerminatorKind.Conditional, entry.Terminator.Kind);
        Assert.Same(@if.Condition, entry.Terminator.Condition);
        Assert.Same(thenBlock, entry.Terminator.TrueSuccessor);
        Assert.Same(elseBlock, entry.Terminator.FalseSuccessor);

        Assert.Empty(thenBlock.Statements);
        Assert.Equal(ControlFlowTerminatorKind.Return, thenBlock.Terminator.Kind);
        Assert.Same(thenReturn.Expression, thenBlock.Terminator.Expression);
        Assert.Empty(thenBlock.Successors);

        Assert.Empty(elseBlock.Statements);
        Assert.Equal(ControlFlowTerminatorKind.Return, elseBlock.Terminator.Kind);
        Assert.Same(elseReturn.Expression, elseBlock.Terminator.Expression);
        Assert.Empty(elseBlock.Successors);

        Assert.Equal(ControlFlowTerminatorKind.FallThrough, join.Terminator.Kind);
        Assert.Empty(join.Successors);
    }

    private static void AssertJump(BasicBlock block, BasicBlock destination)
    {
        Assert.Equal(ControlFlowTerminatorKind.Jump, block.Terminator.Kind);
        Assert.Same(destination, block.Terminator.Destination);
        Assert.Same(destination, Assert.Single(block.Successors));
    }

    private static (ControlFlowGraph Graph, BoundFunction Lowered) BuildGraph(
        string source,
        string scriptName = "Foo",
        string functionName = "Bar",
        PapyrusDialect? dialect = null)
    {
        var compilation = Compilation(dialect ?? PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        var function = Assert.IsAssignableFrom<IFunctionSymbol>(
            Assert.Single(script.GetMembers(compilation.Names.Get(functionName))));
        var lowered = Lowering.LowerFunction(Binder.BindFunction(function));
        return (ControlFlowGraph.Build(lowered), lowered);
    }

    private static PapyrusCompilation Compilation(
        PapyrusDialect dialect,
        string name,
        string source)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse(name)] = SourceText.From(source),
        };
        return PapyrusCompilation.Create(dialect, new TestScriptResolver(sources));
    }
}
