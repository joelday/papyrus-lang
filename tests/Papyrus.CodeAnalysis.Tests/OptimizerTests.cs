using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class OptimizerTests
{
    [Fact]
    public void Emit_optimize_on_is_deterministic()
    {
        const string source =
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  x = x\n" +
            "  Return x\n" +
            "EndFunction\n";

        var first = PexWriter.Write(EmitFile(source, optimize: true));
        var second = PexWriter.Write(EmitFile(source, optimize: true));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Emit_optimize_removes_identity_assign()
    {
        const string source =
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  x = x\n" +
            "  Return x\n" +
            "EndFunction\n";

        var off = Function(EmitFile(source, optimize: false));
        var on = Function(EmitFile(source, optimize: true));
        var defaultEmit = Function(EmitFile(source));

        Assert.True(CountIdentityAssigns(off) >= 1);
        Assert.True(CountIdentityAssigns(defaultEmit) >= 1);
        Assert.Equal(0, CountIdentityAssigns(on));
        Assert.True(on.Instructions.Length < off.Instructions.Length);
        Assert.Contains(on.Instructions, static i => i.OpCode == PexOpCode.Return);
        Assert.Equal(0, CountIdentityAssigns(FunctionFromObject(EmitObject(source, optimize: true))));
    }

    [Fact]
    public void Optimize_removes_identity_assign_and_keeps_nop()
    {
        var x = Id("x");
        var function = FunctionOf(
            Inst(PexOpCode.Assign, x, x),
            Inst(PexOpCode.Nop),
            Inst(PexOpCode.Return, PexValue.None));

        PexOptimizer.Optimize(function);

        Assert.Equal(2, function.Instructions.Length);
        Assert.Equal(PexOpCode.Nop, function.Instructions[0].OpCode);
        Assert.Equal(PexOpCode.Return, function.Instructions[1].OpCode);
    }

    [Fact]
    public void Optimize_removes_forward_jump_over_dead_instructions()
    {
        var x = Id("x");
        var function = FunctionOf(
            Inst(PexOpCode.Jmp, Integer(2)),
            Inst(PexOpCode.Assign, x, x),
            Inst(PexOpCode.Return, PexValue.None));

        PexOptimizer.Optimize(function);

        Assert.Equal(PexOpCode.Return, Assert.Single(function.Instructions).OpCode);
    }

    [Fact]
    public void Optimize_rewrites_jump_offsets_after_deletion()
    {
        var cond = Id("cond");
        var x = Id("x");
        var function = FunctionOf(
            Inst(PexOpCode.JmpF, cond, Integer(3)),
            Inst(PexOpCode.Assign, x, x),
            Inst(PexOpCode.Assign, x, Integer(1)),
            Inst(PexOpCode.Return, PexValue.None));

        PexOptimizer.Optimize(function);

        Assert.Equal(3, function.Instructions.Length);
        Assert.Equal(PexOpCode.JmpF, function.Instructions[0].OpCode);
        Assert.Equal(PexOpCode.Assign, function.Instructions[1].OpCode);
        Assert.Equal(PexOpCode.Return, function.Instructions[2].OpCode);

        Assert.True(PexOpCodeInfo.TryGetBranchOffset(function.Instructions[0], out var offset));
        Assert.Equal(2, offset);
    }

    [Fact]
    public void Optimize_rewrites_backward_jump_after_deletion()
    {
        var x = Id("x");
        var function = FunctionOf(
            Inst(PexOpCode.Assign, x, Integer(1)),
            Inst(PexOpCode.Assign, x, x),
            Inst(PexOpCode.Jmp, Integer(-2)));

        PexOptimizer.Optimize(function);

        Assert.Equal(2, function.Instructions.Length);
        Assert.Equal(PexOpCode.Assign, function.Instructions[0].OpCode);
        Assert.Equal(PexOpCode.Jmp, function.Instructions[1].OpCode);
        Assert.True(PexOpCodeInfo.TryGetBranchOffset(function.Instructions[1], out var offset));
        Assert.Equal(-1, offset);
    }

    [Fact]
    public void Optimize_removes_forward_jump_over_nop_and_keeps_nop()
    {
        var function = FunctionOf(
            Inst(PexOpCode.Jmp, Integer(2)),
            Inst(PexOpCode.Nop),
            Inst(PexOpCode.Return, PexValue.None));

        PexOptimizer.Optimize(function);

        Assert.Equal(2, function.Instructions.Length);
        Assert.Equal(PexOpCode.Nop, function.Instructions[0].OpCode);
        Assert.Equal(PexOpCode.Return, function.Instructions[1].OpCode);
    }

    [Fact]
    public void Optimize_rewrites_debug_line_map_and_does_not_strip_debug_info()
    {
        var x = Id("x");
        var function = new PexFunction
        {
            Name = "Bar",
            Instructions = ImmutableArray.Create(
                Inst(PexOpCode.Assign, x, x),
                Inst(PexOpCode.Return, PexValue.None)),
        };
        var debugFunction = new PexDebugFunction
        {
            ObjectName = "Foo",
            StateName = "",
            FunctionName = "Bar",
            FunctionType = PexDebugFunctionType.Normal,
            InstructionLineMap = ImmutableArray.Create((ushort)10, (ushort)11),
        };
        var file = new PexFile
        {
            DebugInfo = new PexDebugInfo
            {
                Functions = ImmutableArray.Create(debugFunction),
            },
            Objects = ImmutableArray.Create(new PexObject
            {
                Name = "Foo",
                States = ImmutableArray.Create(new PexState
                {
                    Name = "",
                    Functions = ImmutableArray.Create(function),
                }),
            }),
        };

        PexOptimizer.Optimize(file);

        Assert.NotNull(file.DebugInfo);
        var remaining = Assert.Single(file.DebugInfo.Functions);
        Assert.Same(debugFunction, remaining);
        Assert.Equal(PexOpCode.Return, Assert.Single(function.Instructions).OpCode);
        Assert.Equal(ImmutableArray.Create((ushort)11), remaining.InstructionLineMap);
    }

    [Fact]
    public void Optimize_property_accessor_identity_assign()
    {
        var x = Id("x");
        var getter = new PexFunction
        {
            Name = "Get",
            Instructions = ImmutableArray.Create(
                Inst(PexOpCode.Assign, x, x),
                Inst(PexOpCode.Return, x)),
        };
        var obj = new PexObject
        {
            Name = "Foo",
            Properties = ImmutableArray.Create(new PexProperty
            {
                Name = "Health",
                ReadFunction = getter,
            }),
        };

        PexOptimizer.Optimize(obj);

        Assert.Equal(PexOpCode.Return, Assert.Single(getter.Instructions).OpCode);
        Assert.Equal(PexValueType.Identifier, getter.Instructions[0].Arguments[0].Type);
        Assert.Equal("x", getter.Instructions[0].Arguments[0].Text);
    }

    private static int CountIdentityAssigns(PexFunction function)
    {
        var count = 0;
        foreach (var instruction in function.Instructions)
        {
            if (instruction.OpCode != PexOpCode.Assign || instruction.Arguments.Length < 2)
            {
                continue;
            }

            var dest = instruction.Arguments[0];
            var src = instruction.Arguments[1];
            if (dest.Type == PexValueType.Identifier
                && src.Type == PexValueType.Identifier
                && string.Equals(dest.Text, src.Text, System.StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static PexFunction FunctionOf(params PexInstruction[] instructions)
        => new() { Name = "Bar", Instructions = ImmutableArray.Create(instructions) };

    private static PexInstruction Inst(PexOpCode op, params PexValue[] arguments)
        => new()
        {
            OpCode = op,
            Arguments = arguments.Length == 0
                ? ImmutableArray<PexValue>.Empty
                : ImmutableArray.Create(arguments),
        };

    private static PexValue Id(string name) => PexFunctionBuilder.Identifier(name);

    private static PexValue Integer(int value) => PexFunctionBuilder.Integer(value);

    private static PexFunction Function(PexFile file, string name = "Bar")
    {
        var obj = Assert.Single(file.Objects);
        foreach (var state in obj.States)
        {
            if (state.Name is not (null or ""))
            {
                continue;
            }

            foreach (var function in state.Functions)
            {
                if (string.Equals(function.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return function;
                }
            }
        }

        throw new System.InvalidOperationException("Function '" + name + "' was not emitted.");
    }

    private static PexFunction FunctionFromObject(PexObject obj, string name = "Bar")
    {
        foreach (var state in obj.States)
        {
            if (state.Name is not (null or ""))
            {
                continue;
            }

            foreach (var function in state.Functions)
            {
                if (string.Equals(function.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return function;
                }
            }
        }

        throw new System.InvalidOperationException("Function '" + name + "' was not emitted.");
    }

    private static PexFile EmitFile(string source, bool? optimize = null, string scriptName = "Foo")
    {
        var compilation = Compilation(PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        var emitter = optimize is { } value
            ? new PexEmitter(optimize: value)
            : new PexEmitter();
        return emitter.EmitFile(script);
    }

    private static PexObject EmitObject(string source, bool optimize, string scriptName = "Foo")
    {
        var compilation = Compilation(PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        return new PexEmitter(optimize: optimize).Emit(script);
    }

    private static PapyrusCompilation Compilation(PapyrusDialect dialect, string name, string source)
    {
        var sources = new Dictionary<QualifiedName, SourceText>
        {
            [QualifiedName.Parse(name)] = SourceText.From(source),
        };
        return PapyrusCompilation.Create(dialect, new TestScriptResolver(sources));
    }
}
