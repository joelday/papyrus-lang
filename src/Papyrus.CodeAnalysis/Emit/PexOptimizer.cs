using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Emit;

/// <summary>
/// Peephole pass over emitted PEX instructions. Rules match Caprica
/// <c>Caprica/pex/PexOptimizer.cpp</c> at e4dee08. Off by default on
/// <see cref="PexEmitter"/>; pass <c>optimize: true</c> to enable.
/// </summary>
internal static class PexOptimizer
{
    public static void Optimize(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var objects = file.Objects;
        if (objects.IsDefaultOrEmpty)
        {
            return;
        }

        for (var i = 0; i < objects.Length; i++)
        {
            Optimize(objects[i], file.DebugInfo);
        }
    }

    public static void Optimize(PexObject obj, PexDebugInfo? debugInfo = null)
    {
        ArgumentNullException.ThrowIfNull(obj);
        var states = obj.States;
        if (!states.IsDefaultOrEmpty)
        {
            for (var s = 0; s < states.Length; s++)
            {
                var state = states[s];
                var functions = state.Functions;
                if (functions.IsDefaultOrEmpty)
                {
                    continue;
                }

                for (var f = 0; f < functions.Length; f++)
                {
                    var function = functions[f];
                    Optimize(
                        function,
                        FindDebug(debugInfo, obj.Name, state.Name, function.Name, PexDebugFunctionType.Normal));
                }
            }
        }

        var properties = obj.Properties;
        if (properties.IsDefaultOrEmpty)
        {
            return;
        }

        for (var p = 0; p < properties.Length; p++)
        {
            var property = properties[p];
            if (property.ReadFunction is { } getter)
            {
                Optimize(
                    getter,
                    FindDebug(debugInfo, obj.Name, "", property.Name, PexDebugFunctionType.Getter));
            }

            if (property.WriteFunction is { } setter)
            {
                Optimize(
                    setter,
                    FindDebug(debugInfo, obj.Name, "", property.Name, PexDebugFunctionType.Setter));
            }
        }
    }

    public static void Optimize(PexFunction function, PexDebugFunction? debug = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        var instructions = function.Instructions;
        if (instructions.IsDefaultOrEmpty)
        {
            return;
        }

        var lineMap = debug is not null && !debug.InstructionLineMap.IsDefault
            ? debug.InstructionLineMap
            : ImmutableArray<ushort>.Empty;
        var optimized = Build(instructions, lineMap);

        for (var i = optimized.Count - 1; i >= 0; i--)
        {
            var opt = optimized[i];
            if (opt.Instr is null)
            {
                continue;
            }

            switch (opt.Instr.OpCode)
            {
                case PexOpCode.Assign:
                    if (opt.Instr.Arguments.Length >= 2
                        && ValuesEqual(opt.Instr.Arguments[0], opt.Instr.Arguments[1]))
                    {
                        opt.Kill();
                    }

                    break;
                case PexOpCode.Jmp:
                    if (opt.BranchTarget is { } target
                        && opt.Id < target.Id
                        && IsDeadBetween(optimized, opt.Id, target.Id))
                    {
                        opt.Kill();
                    }

                    break;
            }
        }

        var live = 0;
        for (var i = 0; i < optimized.Count; i++)
        {
            optimized[i].InstructionNum = live;
            if (optimized[i].Instr is not null)
            {
                live++;
            }
        }

        if (live == 0)
        {
            function.Instructions = ImmutableArray<PexInstruction>.Empty;
            if (debug is not null)
            {
                debug.InstructionLineMap = ImmutableArray<ushort>.Empty;
            }

            return;
        }

        var rebuilt = ImmutableArray.CreateBuilder<PexInstruction>(live);
        var lines = debug is not null ? ImmutableArray.CreateBuilder<ushort>(live) : null;
        for (var i = 0; i < optimized.Count; i++)
        {
            var opt = optimized[i];
            if (opt.Instr is null)
            {
                continue;
            }

            rebuilt.Add(opt.Instr);
            lines?.Add(opt.LineNumber);
            if (PexOpCodeInfo.IsBranch(opt.Instr.OpCode) && opt.BranchTarget is { } target)
            {
                PexOpCodeInfo.SetBranchOffset(opt.Instr, target.InstructionNum - opt.InstructionNum);
            }
        }

        function.Instructions = rebuilt.MoveToImmutable();
        if (debug is not null && lines is not null)
        {
            debug.InstructionLineMap = lines.MoveToImmutable();
        }
    }

    private static List<OptInstruction> Build(
        ImmutableArray<PexInstruction> instructions,
        ImmutableArray<ushort> lineMap)
    {
        var count = instructions.Length;
        var labels = new Dictionary<int, OptInstruction>();
        for (var i = 0; i < count; i++)
        {
            if (!TryTargetIndex(instructions[i], i, count, out var target)
                || labels.ContainsKey(target))
            {
                continue;
            }

            labels.Add(target, new OptInstruction());
        }

        var result = new List<OptInstruction>(count + labels.Count);
        var id = 0;
        for (var i = 0; i < count; i++, id++)
        {
            if (labels.TryGetValue(i, out var label))
            {
                label.Id = id++;
                result.Add(label);
            }

            var opt = new OptInstruction
            {
                Id = id,
                Instr = instructions[i],
            };
            if ((uint)i < (uint)lineMap.Length)
            {
                opt.LineNumber = lineMap[i];
            }

            if (TryTargetIndex(instructions[i], i, count, out var target)
                && labels.TryGetValue(target, out var dest))
            {
                opt.BranchTarget = dest;
            }

            result.Add(opt);
        }

        if (labels.TryGetValue(count, out var endLabel))
        {
            endLabel.Id = result.Count;
            result.Add(endLabel);
        }

        return result;
    }

    private static bool TryTargetIndex(PexInstruction instruction, int index, int count, out int target)
    {
        target = 0;
        if (!PexOpCodeInfo.TryGetBranchOffset(instruction, out var offset))
        {
            return false;
        }

        var dest = index + offset;
        if ((uint)dest > (uint)count)
        {
            return false;
        }

        target = dest;
        return true;
    }

    private static bool IsDeadBetween(List<OptInstruction> instructions, int startId, int endId)
    {
        if ((uint)endId >= (uint)instructions.Count)
        {
            return false;
        }

        for (var i = startId + 1; i <= endId; i++)
        {
            if (!instructions[i].IsDead)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValuesEqual(PexValue left, PexValue right)
    {
        if (left.Type != right.Type)
        {
            return false;
        }

        switch (left.Type)
        {
            case PexValueType.None:
                return true;
            case PexValueType.Identifier:
            case PexValueType.String:
                return string.Equals(left.Text, right.Text, StringComparison.Ordinal);
            case PexValueType.Integer:
                return left.Integer == right.Integer;
            case PexValueType.Float:
                return left.Float == right.Float;
            case PexValueType.Bool:
                return left.Bool == right.Bool;
            default:
                return false;
        }
    }

    private static PexDebugFunction? FindDebug(
        PexDebugInfo? debugInfo,
        string objectName,
        string stateName,
        string functionName,
        PexDebugFunctionType functionType)
    {
        if (debugInfo is null)
        {
            return null;
        }

        var functions = debugInfo.Functions;
        if (functions.IsDefaultOrEmpty)
        {
            return null;
        }

        for (var i = 0; i < functions.Length; i++)
        {
            var function = functions[i];
            if (function.FunctionType != functionType)
            {
                continue;
            }

            if (!string.Equals(function.ObjectName, objectName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(function.StateName, stateName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(function.FunctionName, functionName, StringComparison.Ordinal))
            {
                continue;
            }

            return function;
        }

        return null;
    }

    private sealed class OptInstruction
    {
        public int Id;
        public int InstructionNum;
        public PexInstruction? Instr;
        public OptInstruction? BranchTarget;
        public ushort LineNumber;

        public bool IsDead => Instr is null || Instr.OpCode == PexOpCode.Nop;

        public void Kill()
        {
            Instr = null;
            BranchTarget = null;
            LineNumber = 0;
        }
    }
}
