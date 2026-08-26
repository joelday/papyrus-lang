using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.CodeAnalysis;
using Papyrus.CodeAnalysis.Emit;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;
using Xunit;

namespace Papyrus.CodeAnalysis.Tests;

public sealed class EmitTests
{
    [Fact]
    public void Emit_tiny_skyrim_script_round_trips_and_disassembles()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Int x = 1\n  x = x + 2\n  Return x\nEndFunction\n");

        var bytes = PexWriter.Write(file);
        var roundTripped = PexReader.Read(bytes);
        Assert.Equal(bytes, PexWriter.Write(roundTripped));

        var pas = PasDisassembler.Disassemble(roundTripped);
        Assert.False(string.IsNullOrWhiteSpace(pas));
        Assert.Contains("iadd", pas, System.StringComparison.OrdinalIgnoreCase);

        var function = Function(roundTripped, "Bar");
        Assert.False(function.IsNative);
        Assert.Contains(function.Instructions, static i => i.OpCode == PexOpCode.IAdd);
        Assert.Contains(function.Instructions, static i => i.OpCode == PexOpCode.Return);
        Assert.Contains(function.Locals, static local => local.Name == "x");
    }

    [Fact]
    public void Emit_if_else_has_conditional_jump_and_resolvable_offsets()
    {
        var function = Function(EmitFile(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  Else\n" +
            "    x = 3\n" +
            "  EndIf\n" +
            "EndFunction\n"));

        var instructions = function.Instructions;
        Assert.Contains(instructions, static i => i.OpCode is PexOpCode.JmpT or PexOpCode.JmpF);
        Assert.Contains(instructions, static i => i.OpCode == PexOpCode.Jmp);

        for (var i = 0; i < instructions.Length; i++)
        {
            if (!TryJumpTarget(instructions[i], i, out var target))
            {
                continue;
            }

            Assert.InRange(target, 0, instructions.Length);
            Assert.NotEqual(i, target);
        }
    }

    [Fact]
    public void Emit_while_has_back_edge()
    {
        var function = Function(EmitFile(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  While x\n" +
            "    x = 0\n" +
            "  EndWhile\n" +
            "EndFunction\n"));

        var instructions = function.Instructions;
        Assert.Contains(instructions, static i => i.OpCode is PexOpCode.JmpT or PexOpCode.JmpF);

        var backEdge = false;
        for (var i = 0; i < instructions.Length; i++)
        {
            if (instructions[i].OpCode == PexOpCode.Jmp
                && TryJumpTarget(instructions[i], i, out var target))
            {
                Assert.InRange(target, 0, instructions.Length);
                if (target < i)
                {
                    backEdge = true;
                }
            }
        }

        Assert.True(backEdge);
    }

    [Fact]
    public void Emit_native_function_has_empty_instructions()
    {
        var function = Function(EmitFile("ScriptName Foo\nFunction Bar() Native\n"));

        Assert.True(function.IsNative);
        Assert.True(function.Instructions.IsDefaultOrEmpty || function.Instructions.IsEmpty);
        Assert.True(function.Locals.IsDefaultOrEmpty || function.Locals.IsEmpty);
    }

    [Fact]
    public void Emit_auto_property_emits_backing_variable()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Property Health Auto\nFunction Bar()\nEndFunction\n");

        var obj = Assert.Single(file.Objects);
        Assert.Contains(obj.Variables, static variable => variable.Name == "::Health_var");
        var property = Assert.Single(obj.Properties);
        Assert.Equal("Health", property.Name);
        Assert.True(property.IsAuto);
        Assert.Equal("::Health_var", property.AutoVar);
    }

    [Fact]
    public void Emit_reuses_temp_of_same_type_after_release()
    {
        var function = Function(EmitFile(
            "ScriptName Foo\nFunction Bar()\n" +
            "  If 1 == 2\n" +
            "  EndIf\n" +
            "  If 3 == 4\n" +
            "  EndIf\n" +
            "EndFunction\n"));

        Assert.Contains(function.Locals, static local => local.Name == "::temp0");
        Assert.DoesNotContain(function.Locals, static local => local.Name == "::temp1");

        var compares = 0;
        foreach (var instruction in function.Instructions)
        {
            if (instruction.OpCode != PexOpCode.CmpEq)
            {
                continue;
            }

            compares++;
            Assert.Equal(PexValueType.Identifier, instruction.Arguments[0].Type);
            Assert.Equal("::temp0", instruction.Arguments[0].Text);
        }

        Assert.Equal(2, compares);
    }

    [Fact]
    public void Emit_struct_field_get_and_set_use_struct_opcodes()
    {
        var function = Function(EmitFile(
            "ScriptName Foo\n" +
            "Struct Point\n  Int X\nEndStruct\n" +
            "Function Bar()\n" +
            "  Point p = new Point\n" +
            "  p.X = 1\n" +
            "  Int y = p.X\n" +
            "EndFunction\n",
            dialect: PapyrusDialect.Fallout4));

        Assert.Contains(function.Instructions, static i => i.OpCode == PexOpCode.StructCreate);
        var set = Assert.Single(function.Instructions, static i => i.OpCode == PexOpCode.StructSet);
        Assert.Equal(PexValueType.Identifier, set.Arguments[0].Type);
        Assert.Equal("p", set.Arguments[0].Text);
        Assert.Equal(PexValueType.String, set.Arguments[1].Type);
        Assert.Equal("X", set.Arguments[1].Text);
        Assert.Equal(1, set.Arguments[2].Integer);

        var get = Assert.Single(function.Instructions, static i => i.OpCode == PexOpCode.StructGet);
        Assert.Equal(PexValueType.Identifier, get.Arguments[0].Type);
        Assert.Equal("y", get.Arguments[0].Text);
        Assert.Equal(PexValueType.Identifier, get.Arguments[1].Type);
        Assert.Equal("p", get.Arguments[1].Text);
        Assert.Equal(PexValueType.String, get.Arguments[2].Type);
        Assert.Equal("X", get.Arguments[2].Text);
        Assert.DoesNotContain(function.Instructions, static i => i.OpCode == PexOpCode.CallMethod);
    }

    [Fact]
    public void Emit_array_find_uses_array_find_opcode()
    {
        var function = Function(EmitFile(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int[] a = new Int[2]\n" +
            "  Int i = a.Find(1)\n" +
            "EndFunction\n"));

        var find = Assert.Single(function.Instructions, static i => i.OpCode == PexOpCode.ArrayFindElement);
        Assert.Equal(4, find.Arguments.Length);
        Assert.Equal(PexValueType.Identifier, find.Arguments[0].Type);
        Assert.Equal("a", find.Arguments[0].Text);
        Assert.Equal(PexValueType.Identifier, find.Arguments[1].Type);
        Assert.Equal("i", find.Arguments[1].Text);
        Assert.Equal(1, find.Arguments[2].Integer);
        Assert.Equal(0, find.Arguments[3].Integer);
        Assert.DoesNotContain(function.Instructions, static i => i.OpCode == PexOpCode.CallMethod);
    }

    [Fact]
    public void Emit_default_includes_debug_info_with_function_line_map()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Int x = 1\n  x = x + 2\n  Return x\nEndFunction\n");

        var debug = Assert.IsType<PexDebugInfo>(file.DebugInfo);
        var function = Assert.Single(debug.Functions);
        Assert.Equal("Foo", function.ObjectName);
        Assert.Equal("", function.StateName);
        Assert.Equal("Bar", function.FunctionName);
        Assert.Equal(PexDebugFunctionType.Normal, function.FunctionType);

        var instructions = Function(file).Instructions;
        Assert.Equal(instructions.Length, function.InstructionLineMap.Length);
        Assert.True(instructions.Length >= 1);
        Assert.Equal((ushort)3, function.InstructionLineMap[0]);

        var returnIndex = IndexOf(instructions, static i => i.OpCode == PexOpCode.Return);
        Assert.Equal((ushort)5, function.InstructionLineMap[returnIndex]);
        Assert.True(function.InstructionLineMap[returnIndex] > function.InstructionLineMap[0]);
    }

    [Fact]
    public void Emit_release_omits_debug_info()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Return 1\nEndFunction\n",
            options: new PexEmitOptions { Release = true });

        Assert.Null(file.DebugInfo);
    }

    [Fact]
    public void Emit_no_debug_info_omits_debug_info()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Return 1\nEndFunction\n",
            options: new PexEmitOptions { EmitDebugInfo = false });

        Assert.Null(file.DebugInfo);
    }

    [Fact]
    public void Emit_release_strips_debug_info_even_when_emit_debug_info_is_true()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Return 1\nEndFunction\n",
            options: new PexEmitOptions { Release = true, EmitDebugInfo = true });

        Assert.Null(file.DebugInfo);
    }

    [Fact]
    public void Emit_debug_line_numbers_are_one_based_and_follow_source()
    {
        var file = EmitFile(
            "ScriptName Foo\nFunction Bar()\n" +
            "  Int x = 1\n" +
            "  If x\n" +
            "    x = 2\n" +
            "  Else\n" +
            "    x = 3\n" +
            "  EndIf\n" +
            "  While x\n" +
            "    x = 0\n" +
            "  EndWhile\n" +
            "EndFunction\n");

        var emitted = Function(file);
        var map = Assert.Single(Assert.IsType<PexDebugInfo>(file.DebugInfo).Functions).InstructionLineMap;
        Assert.Equal(emitted.Instructions.Length, map.Length);
        Assert.Equal((ushort)3, map[0]);

        foreach (var line in map)
        {
            Assert.True(line >= 1);
        }

        var ifJump = IndexOf(emitted.Instructions, static i => i.OpCode is PexOpCode.JmpT or PexOpCode.JmpF);
        Assert.Equal((ushort)4, map[ifJump]);

        var thenAssign = IndexOf(emitted.Instructions, static i =>
            i.OpCode == PexOpCode.Assign && i.Arguments.Length >= 2 && i.Arguments[1].Integer == 2);
        Assert.Equal((ushort)5, map[thenAssign]);

        var elseAssign = IndexOf(emitted.Instructions, static i =>
            i.OpCode == PexOpCode.Assign && i.Arguments.Length >= 2 && i.Arguments[1].Integer == 3);
        Assert.Equal((ushort)7, map[elseAssign]);

        var whileJump = LastIndexOf(emitted.Instructions, static i => i.OpCode is PexOpCode.JmpT or PexOpCode.JmpF);
        Assert.Equal((ushort)9, map[whileJump]);

        var whileBody = IndexOf(emitted.Instructions, static i =>
            i.OpCode == PexOpCode.Assign && i.Arguments.Length >= 2 && i.Arguments[1].Integer == 0);
        Assert.Equal((ushort)10, map[whileBody]);
        Assert.True(map[whileBody] > map[0]);
    }

    [Fact]
    public void Emit_call_line_map_uses_call_span_not_last_argument()
    {
        var file = EmitFile(
            "ScriptName Foo\n" +
            "Int Function Helper(Int a, Int b)\n  Return a\nEndFunction\n" +
            "Int Function Bar()\n  Int x = Helper(\n    1,\n    2)\n  Return x\nEndFunction\n");

        var emitted = Function(file);
        var map = DebugFunction(file, "Bar").InstructionLineMap;
        var call = IndexOf(emitted.Instructions, static i => i.OpCode == PexOpCode.CallMethod);
        Assert.Equal((ushort)6, map[call]);
        Assert.DoesNotContain((ushort)8, map);
    }

    [Fact]
    public void Emit_full_property_accessors_use_getter_and_setter_debug_records()
    {
        var file = EmitFile(
            "ScriptName Foo\n" +
            "Int Property Health\n" +
            "  Int Function Get()\n    Return 1\n  EndFunction\n" +
            "  Function Set(Int value)\n  EndFunction\n" +
            "EndProperty\n");

        var property = Assert.Single(Assert.Single(file.Objects).Properties);
        Assert.Equal("Health", property.Name);
        Assert.NotNull(property.ReadFunction);
        Assert.NotNull(property.WriteFunction);

        var debug = Assert.IsType<PexDebugInfo>(file.DebugInfo);
        Assert.Equal(2, debug.Functions.Length);

        var getter = Assert.Single(debug.Functions, static f => f.FunctionType == PexDebugFunctionType.Getter);
        Assert.Equal("Health", getter.FunctionName);
        Assert.Equal("", getter.StateName);
        Assert.Equal(property.ReadFunction.Instructions.Length, getter.InstructionLineMap.Length);
        Assert.DoesNotContain(debug.Functions, static f => f.FunctionName == "Get");

        var setter = Assert.Single(debug.Functions, static f => f.FunctionType == PexDebugFunctionType.Setter);
        Assert.Equal("Health", setter.FunctionName);
        Assert.Equal("", setter.StateName);
        Assert.Equal(property.WriteFunction.Instructions.Length, setter.InstructionLineMap.Length);
        Assert.DoesNotContain(debug.Functions, static f => f.FunctionName == "Set");
    }

    [Fact]
    public void Emit_debug_info_round_trips_through_writer_and_reader()
    {
        var file = EmitFile(
            "ScriptName Foo\nInt Function Bar()\n  Int x = 1\n  Return x\nEndFunction\n");

        var bytes = PexWriter.Write(file);
        var roundTripped = PexReader.Read(bytes);
        Assert.Equal(bytes, PexWriter.Write(roundTripped));

        var original = Assert.Single(Assert.IsType<PexDebugInfo>(file.DebugInfo).Functions);
        var restored = Assert.Single(Assert.IsType<PexDebugInfo>(roundTripped.DebugInfo).Functions);
        Assert.Equal(original.ObjectName, restored.ObjectName);
        Assert.Equal(original.StateName, restored.StateName);
        Assert.Equal(original.FunctionName, restored.FunctionName);
        Assert.Equal(original.FunctionType, restored.FunctionType);
        Assert.Equal(original.InstructionLineMap, restored.InstructionLineMap);
    }

    [Fact]
    public void Emit_time_provider_pin_keeps_debug_info_bytes_stable()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        const string source = "ScriptName Foo\nInt Function Bar()\n  Return 1\nEndFunction\n";
        var first = EmitFile(source, timeProvider: clock);
        var second = EmitFile(source, timeProvider: clock);

        Assert.Equal(PexWriter.Write(first), PexWriter.Write(second));
        Assert.Equal(DateTimeOffset.UnixEpoch, first.CompileTime);
        Assert.Equal(first.CompileTime, Assert.IsType<PexDebugInfo>(first.DebugInfo).ModificationTime);
    }

    [Fact]
    public void Emit_auto_property_unary_minus_initializer_is_negative_one()
    {
        var file = EmitFile(
            "ScriptName Foo\n" +
            "Int Property Health = -1 Auto\n" +
            "Int Count = -1\n" +
            "Function Bar()\nEndFunction\n");

        var obj = Assert.Single(file.Objects);
        var health = Assert.Single(obj.Variables, static variable => variable.Name == "::Health_var");
        Assert.Equal(PexValueType.Integer, health.DefaultValue.Type);
        Assert.Equal(-1, health.DefaultValue.Integer);

        var count = Assert.Single(obj.Variables, static variable => variable.Name == "Count");
        Assert.Equal(PexValueType.Integer, count.DefaultValue.Type);
        Assert.Equal(-1, count.DefaultValue.Integer);
    }

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

    private static PexDebugFunction DebugFunction(PexFile file, string name = "Bar")
    {
        var debug = Assert.IsType<PexDebugInfo>(file.DebugInfo);
        foreach (var function in debug.Functions)
        {
            if (string.Equals(function.FunctionName, name, StringComparison.OrdinalIgnoreCase)
                && function.FunctionType == PexDebugFunctionType.Normal)
            {
                return function;
            }
        }

        throw new InvalidOperationException("Debug function '" + name + "' was not emitted.");
    }

    private static int IndexOf(System.Collections.Immutable.ImmutableArray<PexInstruction> instructions, Predicate<PexInstruction> match)
    {
        for (var i = 0; i < instructions.Length; i++)
        {
            if (match(instructions[i]))
            {
                return i;
            }
        }

        throw new InvalidOperationException("No matching instruction.");
    }

    private static int LastIndexOf(System.Collections.Immutable.ImmutableArray<PexInstruction> instructions, Predicate<PexInstruction> match)
    {
        for (var i = instructions.Length - 1; i >= 0; i--)
        {
            if (match(instructions[i]))
            {
                return i;
            }
        }

        throw new InvalidOperationException("No matching instruction.");
    }

    private static bool TryJumpTarget(PexInstruction instruction, int index, out int target)
    {
        target = 0;
        var args = instruction.Arguments;
        PexValue offset = instruction.OpCode switch
        {
            PexOpCode.Jmp when args.Length >= 1 => args[0],
            PexOpCode.JmpT or PexOpCode.JmpF when args.Length >= 2 => args[1],
            _ => default,
        };
        if (offset.Type != PexValueType.Integer)
        {
            return false;
        }

        target = index + offset.Integer;
        return true;
    }

    private static PexFile EmitFile(
        string source,
        string scriptName = "Foo",
        PapyrusDialect? dialect = null,
        PexEmitOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        var compilation = Compilation(dialect ?? PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        return new PexEmitter(timeProvider: timeProvider, options: options).EmitFile(script);
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
