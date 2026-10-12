using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Emit;

internal sealed class PexLocal
{
    internal PexLocal(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public string Name { get; }

    public string TypeName { get; }

    internal bool InFreeList { get; set; }
}

internal sealed class PexFunctionBuilder
{
    private readonly Dictionary<string, Stack<PexLocal>> _free = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PexLocalVariable> _userLocals = new();
    private readonly Dictionary<string, PexLocalVariable> _userLocalsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PexLocal> _temps = new();
    private readonly List<PexInstruction> _instructions = new();
    private int _nextTempIndex;

    public int InstructionCount => _instructions.Count;

    public PexLocal AllocateTemp(ITypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var typeName = PexEmitType.NameOf(type);
        if (_free.TryGetValue(typeName, out var free) && free.Count > 0)
        {
            var reused = free.Pop();
            reused.InFreeList = false;
            return reused;
        }

        var temp = new PexLocal("::temp" + _nextTempIndex.ToString(CultureInfo.InvariantCulture), typeName);
        _nextTempIndex++;
        _temps.Add(temp);
        return temp;
    }

    public void ReleaseTemp(PexLocal temp)
    {
        ArgumentNullException.ThrowIfNull(temp);
        if (temp.InFreeList)
        {
            return;
        }

        if (!_free.TryGetValue(temp.TypeName, out var free))
        {
            free = new Stack<PexLocal>();
            _free[temp.TypeName] = free;
        }

        temp.InFreeList = true;
        free.Push(temp);
    }

    public int Emit(PexOpCode op, params PexValue[] operands)
    {
        operands ??= [];
        SplitOperands(op, operands, out var arguments, out var variadic);
        _instructions.Add(new PexInstruction
        {
            OpCode = op,
            Arguments = arguments,
            VariadicArguments = variadic,
        });
        return _instructions.Count - 1;
    }

    public void PatchJumpOffset(int instructionIndex, int targetIndex)
    {
        if ((uint)instructionIndex >= (uint)_instructions.Count)
        {
            return;
        }

        var instruction = _instructions[instructionIndex];
        if (!PexOpCodeInfo.IsBranch(instruction.OpCode))
        {
            return;
        }

        PexOpCodeInfo.SetBranchOffset(instruction, targetIndex - instructionIndex);
    }

    public void AddUserLocal(string name, string typeName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeName);
        if (_userLocalsByName.ContainsKey(name))
        {
            return;
        }

        var local = new PexLocalVariable { Name = name, TypeName = typeName };
        _userLocalsByName.Add(name, local);
        _userLocals.Add(local);
    }

    public ImmutableArray<PexLocalVariable> GetLocals()
    {
        var count = _userLocals.Count + _temps.Count;
        if (count == 0)
        {
            return ImmutableArray<PexLocalVariable>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<PexLocalVariable>(count);
        for (var i = 0; i < _userLocals.Count; i++)
        {
            builder.Add(_userLocals[i]);
        }

        for (var i = 0; i < _temps.Count; i++)
        {
            var temp = _temps[i];
            builder.Add(new PexLocalVariable { Name = temp.Name, TypeName = temp.TypeName });
        }

        return builder.MoveToImmutable();
    }

    public ImmutableArray<PexInstruction> GetInstructions()
        => _instructions.Count == 0
            ? ImmutableArray<PexInstruction>.Empty
            : _instructions.ToImmutableArray();

    public static PexValue Identifier(string name)
        => new() { Type = PexValueType.Identifier, Text = name };

    public static PexValue String(string value)
        => new() { Type = PexValueType.String, Text = value };

    public static PexValue Integer(int value)
        => new() { Type = PexValueType.Integer, Integer = value };

    public static PexValue Float(float value)
        => new() { Type = PexValueType.Float, Float = value };

    public static PexValue Bool(bool value)
        => new() { Type = PexValueType.Bool, Bool = value };

    private static void SplitOperands(
        PexOpCode op,
        PexValue[] operands,
        out ImmutableArray<PexValue> arguments,
        out ImmutableArray<PexValue> variadic)
    {
        var fixedCount = PexOpCodeInfo.FixedArgumentCount(op);
        var hasVariadic = PexOpCodeInfo.HasVariadicArguments(op);
        if (fixedCount == 0)
        {
            arguments = ImmutableArray<PexValue>.Empty;
        }
        else
        {
            var args = ImmutableArray.CreateBuilder<PexValue>(fixedCount);
            for (var i = 0; i < fixedCount; i++)
            {
                args.Add(i < operands.Length ? operands[i] : PexValue.None);
            }

            arguments = args.MoveToImmutable();
        }

        if (!hasVariadic || operands.Length <= fixedCount)
        {
            variadic = ImmutableArray<PexValue>.Empty;
            return;
        }

        var extra = ImmutableArray.CreateBuilder<PexValue>(operands.Length - fixedCount);
        for (var i = fixedCount; i < operands.Length; i++)
        {
            extra.Add(operands[i]);
        }

        variadic = extra.MoveToImmutable();
    }
}

internal static class PexEmitType
{
    public static string NameOf(ITypeSymbol? type)
    {
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            return "None";
        }

        // CustomEventName / ScriptEventName are strings in PEX (Caprica's getTypeStringAsRef).
        if (IntrinsicTypeNames.IsEventName(type, out _))
        {
            return "String";
        }

        var name = type.TypeName.ToString();
        return name.Length == 0 ? "None" : name;
    }

    public static bool IsNamed(ITypeSymbol? type, string name)
        => type is not null
            && type.TypeKind != TypeKind.Error
            && type.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase);
}
