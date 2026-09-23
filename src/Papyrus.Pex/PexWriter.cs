using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace Papyrus.Pex;

/// <summary>
/// Little-endian PEX writer. Deterministic: <c>compilationTime</c> and debug
/// modification time are taken from the <see cref="PexFile"/> (unix 0 unless set).
/// No ambient clock is consulted.
/// </summary>
/// <remarks>
/// Champollion reader contract tests land in P3.09. This assembly has no
/// Champollion dependency.
/// </remarks>
public static class PexWriter
{
    public static byte[] Write(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var stream = new MemoryStream();
        Write(file, stream);
        return stream.ToArray();
    }

    public static void Write(PexFile file, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        new Emitter(file).WriteTo(stream);
    }

    private sealed class Emitter
    {
        private readonly PexFile _file;
        private readonly bool _hasExtendedLayout;
        private readonly bool _hasGuards;
        private readonly List<string> _strings = new();
        private readonly Dictionary<string, ushort> _stringIndex = new(StringComparer.Ordinal);
        private BinaryWriter _writer = null!;

        public Emitter(PexFile file)
        {
            _file = file;

            GameId game = GameIdMapping.Resolve(file.RawGameId, file.Version, file.Game)
                          ?? file.Game;
            _hasExtendedLayout = game is GameId.Fallout4 or GameId.Fallout76 or GameId.Starfield;
            _hasGuards = game == GameId.Starfield;
        }

        public void WriteTo(Stream stream)
        {
            BuildStringTable();

            using var writer = new BinaryWriter(stream, Windows1252Encoding.Instance, leaveOpen: true);
            _writer = writer;
            WriteFile();
            writer.Flush();
        }

        private void WriteFile()
        {
            _writer.Write(PexFile.MagicNumber);
            _writer.Write(PexVersionInfo.GetMajor(_file.Version));
            _writer.Write(PexVersionInfo.GetMinor(_file.Version));
            _writer.Write(RawGameId());
            _writer.Write(ToUnixSeconds(_file.CompileTime));
            WritePrefixedString(_file.SourcePath);
            WritePrefixedString(_file.UserName);
            WritePrefixedString(_file.MachineName);

            WriteStringTable();
            WriteDebugInfo();
            WriteUserFlags();
            WriteObjects();
        }

        private ushort RawGameId()
        {
            return _file.RawGameId != 0
                ? _file.RawGameId
                : GameIdMapping.ToRaw(_file.Game);
        }

        private void WriteStringTable()
        {
            PexLimits.Ensure(_strings.Count, PexLimits.MaxStringTableEntries, "string table");
            WriteCount(_strings.Count, "string table");
            foreach (string value in _strings)
                WritePrefixedString(value);
        }

        private void WriteDebugInfo()
        {
            PexDebugInfo? info = _file.DebugInfo;
            if (info is null)
            {
                _writer.Write((byte)0);
                return;
            }

            _writer.Write((byte)1);
            _writer.Write(ToUnixSeconds(info.ModificationTime));

            ImmutableArray<PexDebugFunction> functions = OrEmpty(info.Functions);
            PexLimits.Ensure(functions.Length, PexLimits.MaxDebugInfoFunctions, "debug-info functions");
            WriteCount(functions.Length, "debug-info functions");
            foreach (PexDebugFunction function in functions)
            {
                WriteStringIndex(function.ObjectName);
                WriteStringIndex(function.StateName);
                WriteStringIndex(function.FunctionName);
                _writer.Write((byte)function.FunctionType);

                ImmutableArray<ushort> lines = OrEmpty(function.InstructionLineMap);
                WriteCount(lines.Length, "debug line map");
                foreach (ushort line in lines)
                    _writer.Write(line);
            }

            if (!_hasExtendedLayout)
                return;

            ImmutableArray<PexDebugPropertyGroup> groups = OrEmpty(info.PropertyGroups);
            WriteCount(groups.Length, "debug property groups");
            foreach (PexDebugPropertyGroup group in groups)
            {
                WriteStringIndex(group.ObjectName);
                WriteStringIndex(group.GroupName);
                WriteStringIndex(group.DocumentationString);
                _writer.Write(group.UserFlags);

                ImmutableArray<string> names = OrEmpty(group.Properties);
                WriteCount(names.Length, "debug property group names");
                foreach (string name in names)
                    WriteStringIndex(name);
            }

            ImmutableArray<PexDebugStructOrder> orders = OrEmpty(info.StructOrders);
            WriteCount(orders.Length, "debug struct orders");
            foreach (PexDebugStructOrder order in orders)
            {
                WriteStringIndex(order.ObjectName);
                WriteStringIndex(order.StructName);

                ImmutableArray<string> members = OrEmpty(order.Members);
                WriteCount(members.Length, "debug struct-order members");
                foreach (string member in members)
                    WriteStringIndex(member);
            }
        }

        private void WriteUserFlags()
        {
            ImmutableArray<PexUserFlag> flags = OrEmpty(_file.UserFlags);
            WriteCount(flags.Length, "user flags");
            foreach (PexUserFlag flag in flags)
            {
                WriteStringIndex(flag.Name);
                _writer.Write(flag.BitIndex);
            }
        }

        private void WriteObjects()
        {
            ImmutableArray<PexObject> objects = OrEmpty(_file.Objects);
            PexLimits.Ensure(objects.Length, PexLimits.MaxObjects, "objects");
            WriteCount(objects.Length, "objects");
            foreach (PexObject obj in objects)
                WriteObject(obj);
        }

        private void WriteObject(PexObject obj)
        {
            WriteStringIndex(obj.Name);
            // Size is whatever the model has (0 unless set). Recomputing it would
            // break write(read(bytes)) == bytes on fixtures that store 0.
            _writer.Write(obj.Size);
            WriteStringIndex(obj.ParentClassName);
            WriteStringIndex(obj.DocumentationString);
            if (_hasExtendedLayout)
                _writer.Write(obj.IsConst ? (byte)1 : (byte)0);
            _writer.Write(obj.UserFlags);
            WriteStringIndex(obj.AutoStateName);

            if (_hasExtendedLayout)
                WriteStructs(OrEmpty(obj.Structs));

            WriteVariables(OrEmpty(obj.Variables));

            if (_hasGuards)
                WriteGuards(OrEmpty(obj.Guards));

            WriteProperties(OrEmpty(obj.Properties));
            WriteStates(OrEmpty(obj.States));
        }

        private void WriteStructs(ImmutableArray<PexStruct> structs)
        {
            WriteCount(structs.Length, "structs");
            foreach (PexStruct struc in structs)
            {
                WriteStringIndex(struc.Name);
                ImmutableArray<PexStructMember> members = OrEmpty(struc.Members);
                WriteCount(members.Length, "struct members");
                foreach (PexStructMember member in members)
                {
                    WriteStringIndex(member.Name);
                    WriteStringIndex(member.TypeName);
                    _writer.Write(member.UserFlags);
                    WriteValue(member.DefaultValue);
                    _writer.Write(member.IsConst ? (byte)1 : (byte)0);
                    WriteStringIndex(member.DocumentationString);
                }
            }
        }

        private void WriteVariables(ImmutableArray<PexVariable> variables)
        {
            WriteCount(variables.Length, "variables");
            foreach (PexVariable variable in variables)
            {
                WriteStringIndex(variable.Name);
                WriteStringIndex(variable.TypeName);
                _writer.Write(variable.UserFlags);
                WriteValue(variable.DefaultValue);
                if (_hasExtendedLayout)
                    _writer.Write(variable.IsConst ? (byte)1 : (byte)0);
            }
        }

        private void WriteGuards(ImmutableArray<PexGuard> guards)
        {
            WriteCount(guards.Length, "guards");
            foreach (PexGuard guard in guards)
                WriteStringIndex(guard.Name);
        }

        private void WriteProperties(ImmutableArray<PexProperty> properties)
        {
            WriteCount(properties.Length, "properties");
            foreach (PexProperty property in properties)
            {
                WriteStringIndex(property.Name);
                WriteStringIndex(property.TypeName);
                WriteStringIndex(property.DocumentationString);
                _writer.Write(property.UserFlags);

                byte flags = 0;
                if (property.IsReadable)
                    flags |= 0x01;
                if (property.IsWritable)
                    flags |= 0x02;
                if (property.IsAuto)
                    flags |= 0x04;
                _writer.Write(flags);

                if (property.IsAuto)
                {
                    WriteStringIndex(property.AutoVar);
                }
                else
                {
                    if (property.IsReadable)
                        WriteFunction(property.ReadFunction, named: false);
                    if (property.IsWritable)
                        WriteFunction(property.WriteFunction, named: false);
                }
            }
        }

        private void WriteStates(ImmutableArray<PexState> states)
        {
            WriteCount(states.Length, "states");
            foreach (PexState state in states)
            {
                WriteStringIndex(state.Name);
                ImmutableArray<PexFunction> functions = OrEmpty(state.Functions);
                WriteCount(functions.Length, "state functions");
                foreach (PexFunction function in functions)
                    WriteFunction(function, named: true);
            }
        }

        private void WriteFunction(PexFunction? function, bool named)
        {
            if (function is null)
                throw new PexFormatException("Missing property accessor function.");

            if (named)
                WriteStringIndex(function.Name);

            WriteStringIndex(function.ReturnTypeName);
            WriteStringIndex(function.DocumentationString);
            _writer.Write(function.UserFlags);

            byte flags = 0;
            if (function.IsGlobal)
                flags |= 0x01;
            if (function.IsNative)
                flags |= 0x02;
            _writer.Write(flags);

            ImmutableArray<PexFunctionParameter> parameters = OrEmpty(function.Parameters);
            WriteCount(parameters.Length, "function parameters");
            foreach (PexFunctionParameter parameter in parameters)
            {
                WriteStringIndex(parameter.Name);
                WriteStringIndex(parameter.TypeName);
            }

            ImmutableArray<PexLocalVariable> locals = OrEmpty(function.Locals);
            WriteCount(locals.Length, "function locals");
            foreach (PexLocalVariable local in locals)
            {
                WriteStringIndex(local.Name);
                WriteStringIndex(local.TypeName);
            }

            WriteInstructions(OrEmpty(function.Instructions));
        }

        private void WriteInstructions(ImmutableArray<PexInstruction> instructions)
        {
            PexLimits.Ensure(instructions.Length, PexLimits.MaxInstructionsPerFunction, "instructions per function");
            WriteCount(instructions.Length, "instructions");
            foreach (PexInstruction instruction in instructions)
            {
                _writer.Write((byte)instruction.OpCode);

                ImmutableArray<PexValue> arguments = OrEmpty(instruction.Arguments);
                foreach (PexValue argument in arguments)
                    WriteValue(argument);

                if (PexOpCodeInfo.HasVariadicArguments(instruction.OpCode))
                {
                    ImmutableArray<PexValue> variadic = OrEmpty(instruction.VariadicArguments);
                    WriteValue(new PexValue { Type = PexValueType.Integer, Integer = variadic.Length });
                    foreach (PexValue argument in variadic)
                        WriteValue(argument);
                }
            }
        }

        private void WriteValue(PexValue value)
        {
            _writer.Write((byte)value.Type);
            switch (value.Type)
            {
                case PexValueType.None:
                    break;
                case PexValueType.Identifier:
                case PexValueType.String:
                    WriteStringIndex(value.Text);
                    break;
                case PexValueType.Integer:
                    _writer.Write(value.Integer);
                    break;
                case PexValueType.Float:
                    _writer.Write(value.Float);
                    break;
                case PexValueType.Bool:
                    _writer.Write(value.Bool ? (byte)1 : (byte)0);
                    break;
                default:
                    throw new PexFormatException($"Unknown PexValue type 0x{(byte)value.Type:X2}.");
            }
        }

        private void WritePrefixedString(string? value)
        {
            byte[] bytes = Windows1252Encoding.Instance.GetBytes(value ?? "");
            if (bytes.Length > ushort.MaxValue)
                throw new PexFormatException($"String exceeds u16 length prefix ({bytes.Length} bytes).");
            _writer.Write((ushort)bytes.Length);
            _writer.Write(bytes);
        }

        private void WriteStringIndex(string? value)
        {
            string text = value ?? "";
            if (!_stringIndex.TryGetValue(text, out ushort index))
                throw new PexFormatException($"String '{text}' is missing from the string table.");
            _writer.Write(index);
        }

        private void WriteCount(int count, string what)
        {
            if ((uint)count > ushort.MaxValue)
                throw new PexFormatException($"{what} count {count} does not fit in a u16.");
            _writer.Write((ushort)count);
        }

        private void BuildStringTable()
        {
            foreach (string existing in OrEmpty(_file.StringTable))
                Intern(existing);

            if (_file.DebugInfo is { } info)
            {
                foreach (PexDebugFunction function in OrEmpty(info.Functions))
                {
                    Intern(function.ObjectName);
                    Intern(function.StateName);
                    Intern(function.FunctionName);
                }

                if (_hasExtendedLayout)
                {
                    foreach (PexDebugPropertyGroup group in OrEmpty(info.PropertyGroups))
                    {
                        Intern(group.ObjectName);
                        Intern(group.GroupName);
                        Intern(group.DocumentationString);
                        foreach (string name in OrEmpty(group.Properties))
                            Intern(name);
                    }

                    foreach (PexDebugStructOrder order in OrEmpty(info.StructOrders))
                    {
                        Intern(order.ObjectName);
                        Intern(order.StructName);
                        foreach (string member in OrEmpty(order.Members))
                            Intern(member);
                    }
                }
            }

            foreach (PexUserFlag flag in OrEmpty(_file.UserFlags))
                Intern(flag.Name);

            foreach (PexObject obj in OrEmpty(_file.Objects))
            {
                Intern(obj.Name);
                Intern(obj.ParentClassName);
                Intern(obj.DocumentationString);
                Intern(obj.AutoStateName);

                if (_hasExtendedLayout)
                {
                    foreach (PexStruct struc in OrEmpty(obj.Structs))
                    {
                        Intern(struc.Name);
                        foreach (PexStructMember member in OrEmpty(struc.Members))
                        {
                            Intern(member.Name);
                            Intern(member.TypeName);
                            InternValue(member.DefaultValue);
                            Intern(member.DocumentationString);
                        }
                    }
                }

                foreach (PexVariable variable in OrEmpty(obj.Variables))
                {
                    Intern(variable.Name);
                    Intern(variable.TypeName);
                    InternValue(variable.DefaultValue);
                }

                if (_hasGuards)
                {
                    foreach (PexGuard guard in OrEmpty(obj.Guards))
                        Intern(guard.Name);
                }

                foreach (PexProperty property in OrEmpty(obj.Properties))
                {
                    Intern(property.Name);
                    Intern(property.TypeName);
                    Intern(property.DocumentationString);
                    if (property.IsAuto)
                    {
                        Intern(property.AutoVar);
                    }
                    else
                    {
                        if (property.IsReadable)
                            InternFunction(property.ReadFunction, named: false);
                        if (property.IsWritable)
                            InternFunction(property.WriteFunction, named: false);
                    }
                }

                foreach (PexState state in OrEmpty(obj.States))
                {
                    Intern(state.Name);
                    foreach (PexFunction function in OrEmpty(state.Functions))
                        InternFunction(function, named: true);
                }
            }
        }

        private void InternFunction(PexFunction? function, bool named)
        {
            if (function is null)
                return;

            if (named)
                Intern(function.Name);
            Intern(function.ReturnTypeName);
            Intern(function.DocumentationString);

            foreach (PexFunctionParameter parameter in OrEmpty(function.Parameters))
            {
                Intern(parameter.Name);
                Intern(parameter.TypeName);
            }

            foreach (PexLocalVariable local in OrEmpty(function.Locals))
            {
                Intern(local.Name);
                Intern(local.TypeName);
            }

            foreach (PexInstruction instruction in OrEmpty(function.Instructions))
            {
                foreach (PexValue argument in OrEmpty(instruction.Arguments))
                    InternValue(argument);
                foreach (PexValue argument in OrEmpty(instruction.VariadicArguments))
                    InternValue(argument);
            }
        }

        private void InternValue(PexValue value)
        {
            if (value.Type is PexValueType.Identifier or PexValueType.String)
                Intern(value.Text);
        }

        private void Intern(string? value)
        {
            string text = value ?? "";
            if (_stringIndex.ContainsKey(text))
                return;

            PexLimits.Ensure(_strings.Count + 1, PexLimits.MaxStringTableEntries, "string table");
            if (_strings.Count >= ushort.MaxValue)
                throw new PexFormatException($"string table count {_strings.Count + 1} does not fit in a u16.");

            _stringIndex[text] = (ushort)_strings.Count;
            _strings.Add(text);
        }

        // DateTimeOffset default is year 1; the on-disk field is unix seconds (0 unless set).
        private static ulong ToUnixSeconds(DateTimeOffset time)
        {
            long seconds = time.ToUnixTimeSeconds();
            return seconds < 0 ? 0UL : (ulong)seconds;
        }

        private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array) =>
            array.IsDefault ? ImmutableArray<T>.Empty : array;
    }
}
