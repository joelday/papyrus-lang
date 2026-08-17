using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Papyrus.Pex;

/// <summary>
/// Emits Caprica/CK-flavor Papyrus assembly (.pas) from a <see cref="PexFile"/>.
/// Deterministic: no ambient clock, LF endings, first-seen jump-label order.
/// Opcode spellings match Caprica <c>PexInstruction::opCodeToPexAsm</c>.
/// </summary>
public static class PasDisassembler
{
    public static string Disassemble(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new Writer(file).Write();
    }

    private sealed class Writer
    {
        private readonly PexFile _file;
        private readonly StringBuilder _sb = new();
        private int _indent;
        private bool _atLineStart = true;

        public Writer(PexFile file) => _file = file;

        public string Write()
        {
            WriteInfo();
            WriteUserFlags();
            WriteObjectTable();
            return _sb.ToString();
        }

        private void WriteInfo()
        {
            WriteLine(".info");
            _indent++;
            WriteQuotedKv("source", _file.SourcePath);
            if (_file.DebugInfo is { } debug)
                WriteKv("modifyTime", ToUnixSeconds(debug.ModificationTime));
            else
                WriteLine(".modifyTime 0 ;Debug info: No");
            WriteKv("compileTime", ToUnixSeconds(_file.CompileTime));
            WriteQuotedKv("user", _file.UserName);
            WriteQuotedKv("computer", _file.MachineName);
            _indent--;
            WriteLine(".endInfo");
        }

        private void WriteUserFlags()
        {
            WriteLine(".userFlagsRef");
            _indent++;
            foreach (PexUserFlag flag in OrEmpty(_file.UserFlags))
                WriteLine($".flag {flag.Name ?? ""} {flag.BitIndex}");
            _indent--;
            WriteLine(".endUserFlagsRef");
        }

        private void WriteObjectTable()
        {
            WriteLine(".objectTable");
            _indent++;
            foreach (PexObject obj in OrEmpty(_file.Objects))
                WriteObject(obj);
            _indent--;
            WriteLine(".endObjectTable");
        }

        private void WriteObject(PexObject obj)
        {
            Write($".object {obj.Name ?? ""} {obj.ParentClassName ?? ""}");
            if (obj.IsConst)
                Write(" const");
            WriteLine();
            _indent++;

            WriteKv("userFlags", obj.UserFlags);
            WriteQuotedKv("docString", obj.DocumentationString);
            WriteLine($".autoState {obj.AutoStateName ?? ""}");

            WriteLine(".structTable");
            _indent++;
            foreach (PexStruct struc in OrEmpty(obj.Structs))
                WriteStruct(struc);
            _indent--;
            WriteLine(".endStructTable");

            WriteLine(".variableTable");
            _indent++;
            foreach (PexVariable variable in OrEmpty(obj.Variables))
                WriteVariable(variable);
            _indent--;
            WriteLine(".endVariableTable");

            WriteLine(".guardTable");
            _indent++;
            foreach (PexGuard guard in OrEmpty(obj.Guards))
                WriteLine($".guard {guard.Name ?? ""}");
            _indent--;
            WriteLine(".endGuardTable");

            WriteLine(".propertyTable");
            _indent++;
            foreach (PexProperty property in OrEmpty(obj.Properties))
                WriteProperty(obj, property);
            _indent--;
            WriteLine(".endPropertyTable");

            WriteLine(".propertyGroupTable");
            _indent++;
            if (_file.DebugInfo is { } debug)
            {
                foreach (PexDebugPropertyGroup group in OrEmpty(debug.PropertyGroups))
                {
                    if (string.Equals(group.ObjectName, obj.Name, StringComparison.Ordinal))
                        WritePropertyGroup(group);
                }
            }
            _indent--;
            WriteLine(".endPropertyGroupTable");

            WriteLine(".stateTable");
            _indent++;
            foreach (PexState state in OrEmpty(obj.States))
                WriteState(obj, state);
            _indent--;
            WriteLine(".endStateTable");

            _indent--;
            WriteLine(".endObject");
        }

        private void WriteStruct(PexStruct struc)
        {
            WriteLine($".struct {struc.Name ?? ""}");
            _indent++;
            foreach (PexStructMember member in OrEmpty(struc.Members))
            {
                Write($".variable {member.Name ?? ""} {member.TypeName ?? ""}");
                if (member.IsConst)
                    Write(" const");
                WriteLine();
                _indent++;
                WriteKv("userFlags", member.UserFlags);
                Write(".initialValue ");
                WriteValue(member.DefaultValue);
                WriteLine();
                WriteQuotedKv("docString", member.DocumentationString);
                _indent--;
                WriteLine(".endVariable");
            }
            _indent--;
            WriteLine(".endStruct");
        }

        private void WriteVariable(PexVariable variable)
        {
            Write($".variable {variable.Name ?? ""} {variable.TypeName ?? ""}");
            if (variable.IsConst)
                Write(" const");
            WriteLine();
            _indent++;
            WriteKv("userFlags", variable.UserFlags);
            Write(".initialValue ");
            WriteValue(variable.DefaultValue);
            WriteLine();
            _indent--;
            WriteLine(".endVariable");
        }

        private void WriteProperty(PexObject obj, PexProperty property)
        {
            Write($".property {property.Name ?? ""} {property.TypeName ?? ""}");
            if (property.IsAuto)
                Write(" auto");
            WriteLine();
            _indent++;

            WriteKv("userFlags", property.UserFlags);
            WriteQuotedKv("docString", property.DocumentationString);
            if (property.IsAuto)
            {
                WriteLine($".autoVar {property.AutoVar ?? ""}");
            }
            else
            {
                if (property.IsReadable && property.ReadFunction is { } getter)
                    WriteFunction(obj, state: null, getter, PexDebugFunctionType.Getter, property.Name ?? "");
                if (property.IsWritable && property.WriteFunction is { } setter)
                    WriteFunction(obj, state: null, setter, PexDebugFunctionType.Setter, property.Name ?? "");
            }

            _indent--;
            WriteLine(".endProperty");
        }

        private void WritePropertyGroup(PexDebugPropertyGroup group)
        {
            WriteLine($".propertyGroup {group.GroupName ?? ""}");
            _indent++;
            WriteKv("userFlags", group.UserFlags);
            WriteQuotedKv("docString", group.DocumentationString);
            foreach (string name in OrEmpty(group.Properties))
                WriteLine($".property {name}");
            _indent--;
            WriteLine(".endPropertyGroup");
        }

        private void WriteState(PexObject obj, PexState state)
        {
            if (string.IsNullOrEmpty(state.Name))
                WriteLine(".state");
            else
                WriteLine($".state {state.Name}");
            _indent++;
            foreach (PexFunction function in OrEmpty(state.Functions))
                WriteFunction(obj, state, function, PexDebugFunctionType.Normal, propertyName: "");
            _indent--;
            WriteLine(".endState");
        }

        private void WriteFunction(
            PexObject obj,
            PexState? state,
            PexFunction function,
            PexDebugFunctionType functionType,
            string propertyName)
        {
            Write(".function ");
            if (functionType == PexDebugFunctionType.Getter)
                Write("get");
            else if (functionType == PexDebugFunctionType.Setter)
                Write("set");
            else
                Write(function.Name ?? "");

            if (function.IsNative)
                Write(" native");
            if (function.IsGlobal)
                Write(" static");
            WriteLine();
            _indent++;

            WriteKv("userFlags", function.UserFlags);
            WriteQuotedKv("docString", function.DocumentationString);
            WriteLine($".return {function.ReturnTypeName ?? ""}");

            WriteLine(".paramTable");
            _indent++;
            foreach (PexFunctionParameter parameter in OrEmpty(function.Parameters))
                WriteLine($".param {parameter.Name ?? ""} {parameter.TypeName ?? ""}");
            _indent--;
            WriteLine(".endParamTable");

            if (!function.IsNative)
            {
                WriteLine(".localTable");
                _indent++;
                foreach (PexLocalVariable local in OrEmpty(function.Locals))
                    WriteLine($".local {local.Name ?? ""} {local.TypeName ?? ""}");
                _indent--;
                WriteLine(".endLocalTable");

                WriteLine(".code");
                _indent++;
                WriteInstructions(obj, state, function, functionType, propertyName);
                _indent--;
                WriteLine(".endCode");
            }

            _indent--;
            WriteLine(".endFunction");
        }

        private void WriteInstructions(
            PexObject obj,
            PexState? state,
            PexFunction function,
            PexDebugFunctionType functionType,
            string propertyName)
        {
            ImmutableArray<PexInstruction> instructions = OrEmpty(function.Instructions);
            Dictionary<int, int> labels = BuildLabelMap(instructions);
            PexDebugFunction? debug = FindDebugFunction(obj, state, function, functionType, propertyName);

            for (int i = 0; i < instructions.Length; i++)
            {
                if (labels.TryGetValue(i, out int labelId))
                    WriteLine($"label{labelId}:");

                PexInstruction instruction = instructions[i];
                Write(OpCodeName(instruction.OpCode));

                ImmutableArray<PexValue> args = OrEmpty(instruction.Arguments);
                if (instruction.OpCode == PexOpCode.Jmp && TryBranchTarget(instruction, i, out int jmpTarget))
                {
                    Write($" label{labels[jmpTarget]}");
                }
                else if ((instruction.OpCode is PexOpCode.JmpT or PexOpCode.JmpF)
                         && TryBranchTarget(instruction, i, out int condTarget))
                {
                    Write(" ");
                    WriteValue(args.Length > 0 ? args[0] : PexValue.None);
                    Write($" label{labels[condTarget]}");
                }
                else
                {
                    foreach (PexValue argument in args)
                    {
                        Write(" ");
                        WriteValue(argument);
                    }

                    foreach (PexValue argument in OrEmpty(instruction.VariadicArguments))
                    {
                        Write(" ");
                        WriteValue(argument);
                    }
                }

                if (debug is not null && i < debug.InstructionLineMap.Length)
                    Write($" ;@line {debug.InstructionLineMap[i]}");

                WriteLine();
            }

            if (labels.TryGetValue(instructions.Length, out int endLabel))
                WriteLine($"label{endLabel}:");
        }

        private PexDebugFunction? FindDebugFunction(
            PexObject obj,
            PexState? state,
            PexFunction function,
            PexDebugFunctionType functionType,
            string propertyName)
        {
            if (_file.DebugInfo is null)
                return null;

            string functionName = propertyName.Length == 0 ? function.Name ?? "" : propertyName;
            string objectName = obj.Name ?? "";
            string stateName = state?.Name ?? "";

            foreach (PexDebugFunction debug in OrEmpty(_file.DebugInfo.Functions))
            {
                if (debug.FunctionType == functionType
                    && string.Equals(debug.ObjectName, objectName, StringComparison.Ordinal)
                    && string.Equals(debug.StateName, stateName, StringComparison.Ordinal)
                    && string.Equals(debug.FunctionName, functionName, StringComparison.Ordinal))
                {
                    return debug;
                }
            }

            return null;
        }

        private void WriteValue(PexValue value)
        {
            switch (value.Type)
            {
                case PexValueType.None:
                    Write("None");
                    break;
                case PexValueType.Identifier:
                    Write(value.Text ?? "");
                    break;
                case PexValueType.String:
                    Write('"');
                    Write(Escape(value.Text ?? ""));
                    Write('"');
                    break;
                case PexValueType.Integer:
                    Write(value.Integer.ToString(CultureInfo.InvariantCulture));
                    break;
                case PexValueType.Float:
                    Write(value.Float.ToString("0.000000", CultureInfo.InvariantCulture));
                    break;
                case PexValueType.Bool:
                    Write(value.Bool ? "True" : "False");
                    break;
                default:
                    Write($"?{value.Type}");
                    break;
            }
        }

        private void WriteQuotedKv(string key, string? value)
        {
            WriteIndent();
            _sb.Append('.').Append(key).Append(" \"").Append(Escape(value ?? "")).Append('"');
            WriteLine();
        }

        private void WriteKv(string key, ulong value)
        {
            WriteLine($".{key} {value.ToString(CultureInfo.InvariantCulture)}");
        }

        private void WriteKv(string key, uint value)
        {
            WriteLine($".{key} {value.ToString(CultureInfo.InvariantCulture)}");
        }

        private void Write(string text)
        {
            WriteIndent();
            _sb.Append(text);
        }

        private void Write(char ch)
        {
            WriteIndent();
            _sb.Append(ch);
        }

        private void WriteLine(string text)
        {
            WriteIndent();
            _sb.Append(text);
            WriteLine();
        }

        private void WriteLine()
        {
            _sb.Append('\n');
            _atLineStart = true;
        }

        private void WriteIndent()
        {
            if (!_atLineStart)
                return;

            for (int i = 0; i < _indent; i++)
                _sb.Append("  ");
            _atLineStart = false;
        }

        private static Dictionary<int, int> BuildLabelMap(ImmutableArray<PexInstruction> instructions)
        {
            var labels = new Dictionary<int, int>();
            int next = 0;
            for (int i = 0; i < instructions.Length; i++)
            {
                if (!TryBranchTarget(instructions[i], i, out int target))
                    continue;
                if (!labels.ContainsKey(target))
                    labels[target] = next++;
            }

            return labels;
        }

        private static bool TryBranchTarget(PexInstruction instruction, int index, out int target)
        {
            target = 0;
            ImmutableArray<PexValue> args = OrEmpty(instruction.Arguments);
            PexValue offset = instruction.OpCode switch
            {
                PexOpCode.Jmp when args.Length >= 1 => args[0],
                PexOpCode.JmpT or PexOpCode.JmpF when args.Length >= 2 => args[1],
                _ => default,
            };

            if (offset.Type != PexValueType.Integer)
                return false;

            long value = (long)index + offset.Integer;
            if (value < int.MinValue || value > int.MaxValue)
                return false;

            target = (int)value;
            return true;
        }

        // DateTimeOffset default is year 1; PAS times are unix seconds (0 unless set).
        private static ulong ToUnixSeconds(DateTimeOffset time)
        {
            long seconds = time.ToUnixTimeSeconds();
            return seconds < 0 ? 0UL : (ulong)seconds;
        }

        private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array) =>
            array.IsDefault ? ImmutableArray<T>.Empty : array;

        private static string Escape(string value)
        {
            if (value.IndexOfAny(['\\', '"', '\n', '\t']) < 0)
                return value;

            var sb = new StringBuilder(value.Length + 8);
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }

    internal static string OpCodeName(PexOpCode op)
    {
        return op switch
        {
            PexOpCode.Nop => "NOOP",
            PexOpCode.IAdd => "IADD",
            PexOpCode.FAdd => "FADD",
            PexOpCode.ISub => "ISUBTRACT",
            PexOpCode.FSub => "FSUBTRACT",
            PexOpCode.IMul => "IMULTIPLY",
            PexOpCode.FMul => "FMULTIPLY",
            PexOpCode.IDiv => "IDIVIDE",
            PexOpCode.FDiv => "FDIVIDE",
            PexOpCode.IMod => "IMOD",
            PexOpCode.Not => "NOT",
            PexOpCode.INeg => "INEGATE",
            PexOpCode.FNeg => "FNEGATE",
            PexOpCode.Assign => "ASSIGN",
            PexOpCode.Cast => "CAST",
            PexOpCode.CmpEq => "COMPAREEQ",
            PexOpCode.CmpLt => "COMPARELT",
            PexOpCode.CmpLte => "COMPARELTE",
            PexOpCode.CmpGt => "COMPAREGT",
            PexOpCode.CmpGte => "COMPAREGTE",
            PexOpCode.Jmp => "JUMP",
            PexOpCode.JmpT => "JUMPT",
            PexOpCode.JmpF => "JUMPF",
            PexOpCode.CallMethod => "CALLMETHOD",
            PexOpCode.CallParent => "CALLPARENT",
            PexOpCode.CallStatic => "CALLSTATIC",
            PexOpCode.Return => "RETURN",
            PexOpCode.StrCat => "STRCAT",
            PexOpCode.PropGet => "PROPGET",
            PexOpCode.PropSet => "PROPSET",
            PexOpCode.ArrayCreate => "ARRAYCREATE",
            PexOpCode.ArrayLength => "ARRAYLENGTH",
            PexOpCode.ArrayGetElement => "ARRAYGETELEMENT",
            PexOpCode.ArraySetElement => "ARRAYSETELEMENT",
            PexOpCode.ArrayFindElement => "ARRAYFINDELEMENT",
            PexOpCode.ArrayRFindElement => "ARRAYRFINDELEMENT",
            PexOpCode.Is => "IS",
            PexOpCode.StructCreate => "STRUCTCREATE",
            PexOpCode.StructGet => "STRUCTGET",
            PexOpCode.StructSet => "STRUCTSET",
            PexOpCode.ArrayFindStruct => "ARRAYFINDSTRUCT",
            PexOpCode.ArrayRFindStruct => "ARRAYRFINDSTRUCT",
            PexOpCode.ArrayAdd => "ARRAYADDELEMENTS",
            PexOpCode.ArrayInsert => "ARRAYINSERTELEMENT",
            PexOpCode.ArrayRemoveLast => "ARRAYREMOVELASTELEMENT",
            PexOpCode.ArrayRemove => "ARRAYREMOVEELEMENTS",
            PexOpCode.ArrayClear => "ARRAYCLEARELEMENTS",
            PexOpCode.ArrayGetAllMatchingStructs => "ARRAYGETALLMATCHINGSTRUCTS",
            PexOpCode.LockGuards => "LOCKGUARDS",
            PexOpCode.UnlockGuards => "UNLOCKGUARDS",
            PexOpCode.TryLockGuards => "TRYLOCKGUARDS",
            PexOpCode.Invalid => "INVALID",
            _ => op.ToString(),
        };
    }
}
