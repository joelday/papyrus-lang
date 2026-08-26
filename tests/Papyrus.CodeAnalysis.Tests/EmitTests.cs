using System.Collections.Generic;
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
        PapyrusDialect? dialect = null)
    {
        var compilation = Compilation(dialect ?? PapyrusDialect.SkyrimSe, scriptName, source);
        var script = compilation.GetScript(QualifiedName.Parse(scriptName));
        return new PexEmitter().EmitFile(script);
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
