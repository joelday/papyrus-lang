using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Papyrus.Pex;

/// <summary>
/// Parses Caprica/CK-flavor Papyrus assembly (.pas) as emitted by
/// <see cref="PasDisassembler"/> back into a <see cref="PexFile"/>.
/// </summary>
public static class PasAssembler
{
    public static PexFile Assemble(string pas)
    {
        ArgumentNullException.ThrowIfNull(pas);
        return new Parser(pas).Parse();
    }

    private sealed class Parser
    {
        private static readonly Dictionary<string, PexOpCode> OpCodes = CreateOpCodeMap();

        private readonly string[] _lines;
        private int _index;
        private bool _hasDebugInfo;
        private ulong _modifyTime;
        private readonly List<PexDebugFunction> _debugFunctions = new();
        private readonly List<PexDebugPropertyGroup> _propertyGroups = new();

        public Parser(string pas)
        {
            _lines = pas.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }

        public PexFile Parse()
        {
            var file = new PexFile();
            ParseInfo(file);
            file.UserFlags = ParseUserFlags();
            file.Objects = ParseObjectTable();
            AttachDebugInfo(file);
            InferGame(file);
            return file;
        }

        private void ParseInfo(PexFile file)
        {
            ExpectDirective(".info");
            while (true)
            {
                Line line = ReadLine();
                switch (line.Directive)
                {
                    case ".source":
                        file.SourcePath = QuotedOrText(line, 0);
                        break;
                    case ".modifyTime":
                        _modifyTime = ParseUInt64(line, 0, "modifyTime");
                        // Disassembler writes this comment only when DebugInfo is absent.
                        _hasDebugInfo = line.Comment is null
                            || !line.Comment.Contains("Debug info: No", StringComparison.Ordinal);
                        break;
                    case ".compileTime":
                        file.CompileTime = DateTimeOffset.FromUnixTimeSeconds(
                            (long)ParseUInt64(line, 0, "compileTime"));
                        break;
                    case ".user":
                        file.UserName = QuotedOrText(line, 0);
                        break;
                    case ".computer":
                        file.MachineName = QuotedOrText(line, 0);
                        break;
                    case ".endInfo":
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .info.");
                }
            }
        }

        private ImmutableArray<PexUserFlag> ParseUserFlags()
        {
            ExpectDirective(".userFlagsRef");
            var flags = ImmutableArray.CreateBuilder<PexUserFlag>();
            while (true)
            {
                Line line = ReadLine();
                switch (line.Directive)
                {
                    case ".flag":
                        flags.Add(new PexUserFlag
                        {
                            Name = TokenText(line, 0),
                            BitIndex = ParseByte(line, 1, "flag bit"),
                        });
                        break;
                    case ".endUserFlagsRef":
                        return flags.ToImmutable();
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .userFlagsRef.");
                }
            }
        }

        private ImmutableArray<PexObject> ParseObjectTable()
        {
            ExpectDirective(".objectTable");
            var objects = ImmutableArray.CreateBuilder<PexObject>();
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".object":
                        PexLimits.Ensure(objects.Count + 1, PexLimits.MaxObjects, "objects");
                        objects.Add(ParseObject());
                        break;
                    case ".endObjectTable":
                        ReadLine();
                        return objects.ToImmutable();
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .objectTable.");
                }
            }
        }

        private PexObject ParseObject()
        {
            Line header = ReadLine();
            ParseObjectHeader(header, out string name, out string parent, out bool isConst);

            var obj = new PexObject
            {
                Name = name,
                ParentClassName = parent,
                IsConst = isConst,
            };

            var structs = ImmutableArray.CreateBuilder<PexStruct>();
            var variables = ImmutableArray.CreateBuilder<PexVariable>();
            var guards = ImmutableArray.CreateBuilder<PexGuard>();
            var properties = ImmutableArray.CreateBuilder<PexProperty>();
            var states = ImmutableArray.CreateBuilder<PexState>();
            int functionsInObject = 0;

            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        obj.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".docString":
                        ReadLine();
                        obj.DocumentationString = QuotedOrText(line, 0);
                        break;
                    case ".autoState":
                        ReadLine();
                        obj.AutoStateName = RemainderAfterDirective(line);
                        break;
                    case ".structTable":
                        ReadLine();
                        ParseStructTable(structs);
                        break;
                    case ".variableTable":
                        ReadLine();
                        ParseVariableTable(variables);
                        break;
                    case ".guardTable":
                        ReadLine();
                        ParseGuardTable(guards);
                        break;
                    case ".propertyTable":
                        ReadLine();
                        ParsePropertyTable(obj.Name, properties, ref functionsInObject);
                        break;
                    case ".propertyGroupTable":
                        ReadLine();
                        ParsePropertyGroupTable(obj.Name);
                        break;
                    case ".stateTable":
                        ReadLine();
                        ParseStateTable(obj.Name, states, ref functionsInObject);
                        break;
                    case ".endObject":
                        ReadLine();
                        obj.Structs = structs.ToImmutable();
                        obj.Variables = variables.ToImmutable();
                        obj.Guards = guards.ToImmutable();
                        obj.Properties = properties.ToImmutable();
                        obj.States = states.ToImmutable();
                        return obj;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .object.");
                }
            }
        }

        private void ParseStructTable(ImmutableArray<PexStruct>.Builder structs)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".struct":
                        structs.Add(ParseStruct());
                        break;
                    case ".endStructTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .structTable.");
                }
            }
        }

        private PexStruct ParseStruct()
        {
            Line header = ReadLine();
            var struc = new PexStruct { Name = TokenText(header, 0) };
            var members = ImmutableArray.CreateBuilder<PexStructMember>();
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".variable":
                        members.Add(ParseStructMember());
                        break;
                    case ".endStruct":
                        ReadLine();
                        struc.Members = members.ToImmutable();
                        return struc;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .struct.");
                }
            }
        }

        private PexStructMember ParseStructMember()
        {
            Line header = ReadLine();
            var member = new PexStructMember
            {
                Name = TokenText(header, 0),
                TypeName = TokenText(header, 1),
                IsConst = HasModifier(header, "const"),
            };

            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        member.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".initialValue":
                        ReadLine();
                        member.DefaultValue = line.Args.Count > 0 ? ToValue(line.Args[0]) : PexValue.None;
                        break;
                    case ".docString":
                        ReadLine();
                        member.DocumentationString = QuotedOrText(line, 0);
                        break;
                    case ".endVariable":
                        ReadLine();
                        return member;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in struct .variable.");
                }
            }
        }

        private void ParseVariableTable(ImmutableArray<PexVariable>.Builder variables)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".variable":
                        variables.Add(ParseVariable());
                        break;
                    case ".endVariableTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .variableTable.");
                }
            }
        }

        private PexVariable ParseVariable()
        {
            Line header = ReadLine();
            var variable = new PexVariable
            {
                Name = TokenText(header, 0),
                TypeName = TokenText(header, 1),
                IsConst = HasModifier(header, "const"),
            };

            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        variable.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".initialValue":
                        ReadLine();
                        variable.DefaultValue = line.Args.Count > 0 ? ToValue(line.Args[0]) : PexValue.None;
                        break;
                    case ".endVariable":
                        ReadLine();
                        return variable;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .variable.");
                }
            }
        }

        private void ParseGuardTable(ImmutableArray<PexGuard>.Builder guards)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".guard":
                        ReadLine();
                        guards.Add(new PexGuard { Name = TokenText(line, 0) });
                        break;
                    case ".endGuardTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .guardTable.");
                }
            }
        }

        private void ParsePropertyTable(
            string objectName,
            ImmutableArray<PexProperty>.Builder properties,
            ref int functionsInObject)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".property":
                        properties.Add(ParseProperty(objectName, ref functionsInObject));
                        break;
                    case ".endPropertyTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .propertyTable.");
                }
            }
        }

        private PexProperty ParseProperty(string objectName, ref int functionsInObject)
        {
            Line header = ReadLine();
            bool isAutoReadOnly = HasModifier(header, "autoreadonly");
            bool isAuto = isAutoReadOnly || HasModifier(header, "auto");
            var property = new PexProperty
            {
                Name = TokenText(header, 0),
                TypeName = TokenText(header, 1),
                IsAuto = isAuto,
                IsReadable = isAuto,
                IsWritable = isAuto && !isAutoReadOnly,
            };

            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        property.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".docString":
                        ReadLine();
                        property.DocumentationString = QuotedOrText(line, 0);
                        break;
                    case ".autoVar":
                        ReadLine();
                        property.AutoVar = TokenText(line, 0);
                        property.IsAuto = true;
                        if (!property.IsReadable && !property.IsWritable)
                        {
                            property.IsReadable = true;
                            property.IsWritable = true;
                        }

                        break;
                    case ".function":
                        PexFunction accessor = ParseFunction(
                            objectName,
                            stateName: "",
                            propertyName: property.Name);
                        CountFunction(ref functionsInObject);
                        if (string.Equals(accessor.Name, "set", StringComparison.Ordinal))
                        {
                            // PEX stores accessors unnamed; "get"/"set" is only the PAS keyword.
                            accessor.Name = "";
                            property.WriteFunction = accessor;
                            property.IsWritable = true;
                        }
                        else
                        {
                            accessor.Name = "";
                            property.ReadFunction = accessor;
                            property.IsReadable = true;
                        }

                        break;
                    case ".endProperty":
                        ReadLine();
                        return property;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .property.");
                }
            }
        }

        private void ParsePropertyGroupTable(string objectName)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".propertyGroup":
                        _propertyGroups.Add(ParsePropertyGroup(objectName));
                        break;
                    case ".endPropertyGroupTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .propertyGroupTable.");
                }
            }
        }

        private PexDebugPropertyGroup ParsePropertyGroup(string objectName)
        {
            Line header = ReadLine();
            var group = new PexDebugPropertyGroup
            {
                ObjectName = objectName,
                GroupName = TokenText(header, 0),
            };
            var names = ImmutableArray.CreateBuilder<string>();
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        group.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".docString":
                        ReadLine();
                        group.DocumentationString = QuotedOrText(line, 0);
                        break;
                    case ".property":
                        ReadLine();
                        names.Add(TokenText(line, 0));
                        break;
                    case ".endPropertyGroup":
                        ReadLine();
                        group.Properties = names.ToImmutable();
                        return group;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .propertyGroup.");
                }
            }
        }

        private void ParseStateTable(
            string objectName,
            ImmutableArray<PexState>.Builder states,
            ref int functionsInObject)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".state":
                        states.Add(ParseState(objectName, ref functionsInObject));
                        break;
                    case ".endStateTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .stateTable.");
                }
            }
        }

        private PexState ParseState(string objectName, ref int functionsInObject)
        {
            Line header = ReadLine();
            var state = new PexState { Name = RemainderAfterDirective(header) };
            var functions = ImmutableArray.CreateBuilder<PexFunction>();
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".function":
                        CountFunction(ref functionsInObject);
                        functions.Add(ParseFunction(objectName, state.Name, propertyName: ""));
                        break;
                    case ".endState":
                        ReadLine();
                        state.Functions = functions.ToImmutable();
                        return state;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .state.");
                }
            }
        }

        private PexFunction ParseFunction(string objectName, string stateName, string propertyName)
        {
            Line header = ReadLine();
            var function = new PexFunction
            {
                Name = TokenText(header, 0),
                IsNative = HasModifier(header, "native"),
                IsGlobal = HasModifier(header, "static"),
            };

            var parameters = ImmutableArray.CreateBuilder<PexFunctionParameter>();
            var locals = ImmutableArray.CreateBuilder<PexLocalVariable>();
            ImmutableArray<PexInstruction> instructions = ImmutableArray<PexInstruction>.Empty;
            ushort[]? lineMap = null;

            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".userFlags":
                        ReadLine();
                        function.UserFlags = ParseUInt32(line, 0, "userFlags");
                        break;
                    case ".docString":
                        ReadLine();
                        function.DocumentationString = QuotedOrText(line, 0);
                        break;
                    case ".return":
                        ReadLine();
                        function.ReturnTypeName = TokenText(line, 0);
                        break;
                    case ".paramTable":
                        ReadLine();
                        ParseParamTable(parameters);
                        break;
                    case ".localTable":
                        ReadLine();
                        ParseLocalTable(locals);
                        break;
                    case ".code":
                        ReadLine();
                        (instructions, lineMap) = ParseCode();
                        break;
                    case ".endFunction":
                        ReadLine();
                        function.Parameters = parameters.ToImmutable();
                        function.Locals = locals.ToImmutable();
                        function.Instructions = instructions;
                        if (lineMap is not null)
                        {
                            PexLimits.Ensure(
                                _debugFunctions.Count + 1,
                                PexLimits.MaxDebugInfoFunctions,
                                "debug-info functions");
                            _debugFunctions.Add(new PexDebugFunction
                            {
                                ObjectName = objectName,
                                StateName = stateName,
                                FunctionName = propertyName.Length == 0 ? function.Name : propertyName,
                                FunctionType = propertyName.Length == 0
                                    ? PexDebugFunctionType.Normal
                                    : string.Equals(function.Name, "set", StringComparison.Ordinal)
                                        ? PexDebugFunctionType.Setter
                                        : PexDebugFunctionType.Getter,
                                InstructionLineMap = lineMap.ToImmutableArray(),
                            });
                        }

                        return function;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .function.");
                }
            }
        }

        private void ParseParamTable(ImmutableArray<PexFunctionParameter>.Builder parameters)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".param":
                        ReadLine();
                        parameters.Add(new PexFunctionParameter
                        {
                            Name = TokenText(line, 0),
                            TypeName = TokenText(line, 1),
                        });
                        break;
                    case ".endParamTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .paramTable.");
                }
            }
        }

        private void ParseLocalTable(ImmutableArray<PexLocalVariable>.Builder locals)
        {
            while (true)
            {
                Line line = PeekLine();
                switch (line.Directive)
                {
                    case ".local":
                        ReadLine();
                        locals.Add(new PexLocalVariable
                        {
                            Name = TokenText(line, 0),
                            TypeName = TokenText(line, 1),
                        });
                        break;
                    case ".endLocalTable":
                        ReadLine();
                        return;
                    default:
                        throw Error($"unexpected directive '{line.Directive}' in .localTable.");
                }
            }
        }

        private (ImmutableArray<PexInstruction> Instructions, ushort[]? LineMap) ParseCode()
        {
            var raw = new List<RawInstruction>();
            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            ushort[]? lineMap = null;
            int lastLineIndex = -1;

            while (true)
            {
                if (!TryPeekRaw(out Line line))
                    throw Error("unexpected end of file in .code.");

                if (line.Kind == LineKind.Directive)
                {
                    if (line.Directive != ".endCode")
                        throw Error($"unexpected directive '{line.Directive}' in .code.");
                    ReadLine();
                    break;
                }

                ReadLine();
                if (line.Kind == LineKind.Label)
                {
                    labels[line.Label] = raw.Count;
                    continue;
                }

                PexLimits.Ensure(raw.Count + 1, PexLimits.MaxInstructionsPerFunction, "instructions per function");
                if (!OpCodes.TryGetValue(line.Opcode, out PexOpCode op))
                    throw Error($"unknown opcode '{line.Opcode}'.");

                var operands = new List<Operand>(line.Args.Count);
                foreach (Token token in line.Args)
                    operands.Add(new Operand { Token = token });

                int index = raw.Count;
                raw.Add(new RawInstruction { OpCode = op, Operands = operands });

                if (TryParseLineComment(line.Comment, out ushort sourceLine))
                {
                    lastLineIndex = index;
                    lineMap ??= new ushort[index + 1];
                    if (index >= lineMap.Length)
                    {
                        var grown = new ushort[index + 1];
                        Array.Copy(lineMap, grown, lineMap.Length);
                        lineMap = grown;
                    }

                    lineMap[index] = sourceLine;
                }
            }

            if (lineMap is not null && lastLineIndex >= 0 && lineMap.Length != lastLineIndex + 1)
            {
                var trimmed = new ushort[lastLineIndex + 1];
                Array.Copy(lineMap, trimmed, trimmed.Length);
                lineMap = trimmed;
            }

            var instructions = ImmutableArray.CreateBuilder<PexInstruction>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
                instructions.Add(BindInstruction(raw[i], i, labels));

            return (instructions.MoveToImmutable(), lineMap);
        }

        private PexInstruction BindInstruction(
            RawInstruction raw,
            int index,
            Dictionary<string, int> labels)
        {
            var values = new List<PexValue>(raw.Operands.Count);
            int labelSlot = raw.OpCode switch
            {
                PexOpCode.Jmp => 0,
                PexOpCode.JmpT or PexOpCode.JmpF => 1,
                _ => -1,
            };

            for (int i = 0; i < raw.Operands.Count; i++)
            {
                Token token = raw.Operands[i].Token;
                if (i == labelSlot && !token.Quoted && labels.TryGetValue(token.Text, out int target))
                {
                    long offset = (long)target - index;
                    if (offset < int.MinValue || offset > int.MaxValue)
                        throw Error($"jump offset {offset} from instruction {index} does not fit in i32.");
                    values.Add(new PexValue { Type = PexValueType.Integer, Integer = (int)offset });
                }
                else
                {
                    values.Add(ToValue(token));
                }
            }

            int fixedCount;
            try
            {
                fixedCount = PexOpCodeInfo.FixedArgumentCount(raw.OpCode);
            }
            catch (PexFormatException)
            {
                fixedCount = values.Count;
            }

            if (PexOpCodeInfo.HasVariadicArguments(raw.OpCode))
            {
                int take = Math.Min(fixedCount, values.Count);
                return new PexInstruction
                {
                    OpCode = raw.OpCode,
                    Arguments = Take(values, 0, take),
                    VariadicArguments = Take(values, take, values.Count - take),
                };
            }

            return new PexInstruction
            {
                OpCode = raw.OpCode,
                Arguments = Take(values, 0, values.Count),
            };
        }

        private void AttachDebugInfo(PexFile file)
        {
            if (!_hasDebugInfo && _debugFunctions.Count == 0 && _propertyGroups.Count == 0)
                return;

            file.DebugInfo = new PexDebugInfo
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds((long)_modifyTime),
                Functions = _debugFunctions.ToImmutableArray(),
                PropertyGroups = _propertyGroups.ToImmutableArray(),
            };
        }

        private static void InferGame(PexFile file)
        {
            bool starfield = false;
            bool fo76 = false;
            bool fo4 = false;

            if (file.DebugInfo is { } debug
                && !debug.PropertyGroups.IsDefaultOrEmpty
                && debug.PropertyGroups.Length > 0)
            {
                fo4 = true;
            }

            foreach (PexObject obj in file.Objects)
            {
                if (obj.IsConst)
                    fo4 = true;
                if (!obj.Structs.IsDefaultOrEmpty && obj.Structs.Length > 0)
                    fo4 = true;
                if (!obj.Guards.IsDefaultOrEmpty && obj.Guards.Length > 0)
                    starfield = true;

                foreach (PexVariable variable in obj.Variables)
                {
                    if (variable.IsConst)
                        fo4 = true;
                }

                foreach (PexProperty property in obj.Properties)
                {
                    ClassifyFunction(property.ReadFunction, ref starfield, ref fo76, ref fo4);
                    ClassifyFunction(property.WriteFunction, ref starfield, ref fo76, ref fo4);
                }

                foreach (PexState state in obj.States)
                {
                    foreach (PexFunction function in state.Functions)
                        ClassifyFunction(function, ref starfield, ref fo76, ref fo4);
                }
            }

            if (starfield)
            {
                file.Game = GameId.Starfield;
                file.Version = PexVersion.V312;
                file.RawGameId = GameIdMapping.StarfieldRaw;
            }
            else if (fo76)
            {
                file.Game = GameId.Fallout76;
                file.Version = PexVersion.V315;
                file.RawGameId = GameIdMapping.Fallout76Raw;
            }
            else if (fo4)
            {
                file.Game = GameId.Fallout4;
                file.Version = PexVersion.V39;
                file.RawGameId = GameIdMapping.Fallout4Raw;
            }
            else
            {
                file.Game = GameId.SkyrimSpecialEdition;
                file.Version = PexVersion.V32;
                file.RawGameId = GameIdMapping.SkyrimRaw;
            }
        }

        private static void ClassifyFunction(
            PexFunction? function,
            ref bool starfield,
            ref bool fo76,
            ref bool fo4)
        {
            if (function is null)
                return;

            foreach (PexInstruction instruction in function.Instructions)
            {
                PexOpCode op = instruction.OpCode;
                if (op is PexOpCode.LockGuards or PexOpCode.UnlockGuards or PexOpCode.TryLockGuards)
                    starfield = true;
                else if (op == PexOpCode.ArrayGetAllMatchingStructs)
                    fo76 = true;
                else if (op > PexOpCode.SkyrimMax && op <= PexOpCode.Fallout4Max)
                    fo4 = true;
            }
        }

        private void ExpectDirective(string name)
        {
            Line line = ReadLine();
            if (line.Kind != LineKind.Directive || line.Directive != name)
                throw Error($"expected {name}.");
        }

        private Line PeekLine()
        {
            int saved = _index;
            Line line = ReadLine();
            _index = saved;
            return line;
        }

        private bool TryPeekRaw(out Line line)
        {
            int saved = _index;
            if (!TryReadLine(out line))
                return false;
            _index = saved;
            return true;
        }

        private Line ReadLine()
        {
            if (!TryReadLine(out Line line))
                throw Error("unexpected end of file.");
            return line;
        }

        private bool TryReadLine(out Line line)
        {
            while (_index < _lines.Length)
            {
                string raw = _lines[_index++];
                if (!TryParseLine(raw, out line))
                    continue;
                return true;
            }

            line = default;
            return false;
        }

        private bool TryParseLine(string raw, out Line line)
        {
            line = default;
            List<Token> tokens = Tokenize(raw, out string? comment);
            if (tokens.Count == 0)
                return false;

            Token first = tokens[0];
            if (!first.Quoted && first.Text.StartsWith('.'))
            {
                var args = new List<Token>(tokens.Count - 1);
                for (int i = 1; i < tokens.Count; i++)
                    args.Add(tokens[i]);
                line = new Line
                {
                    Kind = LineKind.Directive,
                    Directive = first.Text,
                    Args = args,
                    Comment = comment,
                    Raw = raw,
                };
                return true;
            }

            if (!first.Quoted && first.Text.EndsWith(':') && tokens.Count == 1)
            {
                line = new Line
                {
                    Kind = LineKind.Label,
                    Label = first.Text[..^1],
                    Args = new List<Token>(),
                    Comment = comment,
                    Raw = raw,
                };
                return true;
            }

            var operands = new List<Token>(tokens.Count - 1);
            for (int i = 1; i < tokens.Count; i++)
                operands.Add(tokens[i]);
            line = new Line
            {
                Kind = LineKind.Instruction,
                Opcode = first.Text,
                Args = operands,
                Comment = comment,
                Raw = raw,
            };
            return true;
        }

        private static List<Token> Tokenize(string raw, out string? comment)
        {
            var tokens = new List<Token>();
            comment = null;
            int i = 0;
            while (i < raw.Length)
            {
                while (i < raw.Length && char.IsWhiteSpace(raw[i]))
                    i++;
                if (i >= raw.Length)
                    break;
                if (raw[i] == ';')
                {
                    comment = raw[(i + 1)..].Trim();
                    break;
                }

                if (raw[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < raw.Length)
                    {
                        char ch = raw[i++];
                        if (ch == '\\' && i < raw.Length)
                        {
                            char escaped = raw[i++];
                            sb.Append(escaped switch
                            {
                                'n' => '\n',
                                't' => '\t',
                                '"' => '"',
                                '\\' => '\\',
                                _ => escaped,
                            });
                        }
                        else if (ch == '"')
                        {
                            break;
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                    }

                    tokens.Add(new Token(sb.ToString(), quoted: true));
                    continue;
                }

                int start = i;
                while (i < raw.Length && !char.IsWhiteSpace(raw[i]) && raw[i] != ';')
                    i++;
                tokens.Add(new Token(raw[start..i], quoted: false));
            }

            return tokens;
        }

        private static PexValue ToValue(Token token)
        {
            if (token.Quoted)
                return new PexValue { Type = PexValueType.String, Text = token.Text };

            if (token.Text == "None")
                return PexValue.None;
            if (token.Text == "True")
                return new PexValue { Type = PexValueType.Bool, Bool = true };
            if (token.Text == "False")
                return new PexValue { Type = PexValueType.Bool, Bool = false };

            if (token.Text.Contains('.')
                && float.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                return new PexValue { Type = PexValueType.Float, Float = f };
            }

            if (int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                return new PexValue { Type = PexValueType.Integer, Integer = n };

            return new PexValue { Type = PexValueType.Identifier, Text = token.Text };
        }

        private static bool TryParseLineComment(string? comment, out ushort line)
        {
            line = 0;
            if (comment is null || !comment.StartsWith("@line ", StringComparison.Ordinal))
                return false;
            return ushort.TryParse(
                comment.AsSpan("@line ".Length).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out line);
        }

        private static void ParseObjectHeader(Line header, out string name, out string parent, out bool isConst)
        {
            string rest = RemainderAfterDirective(header, trim: false);
            isConst = false;
            if (rest.EndsWith(" const", StringComparison.Ordinal))
            {
                isConst = true;
                rest = rest[..^" const".Length];
            }

            int space = rest.IndexOf(' ');
            if (space < 0)
            {
                name = rest;
                parent = "";
            }
            else
            {
                name = rest[..space];
                parent = rest[(space + 1)..].Trim();
            }
        }

        private static string RemainderAfterDirective(Line line, bool trim = true)
        {
            string raw = line.Raw.Trim();
            string directive = line.Directive;
            string rest = raw.Length >= directive.Length && raw.StartsWith(directive, StringComparison.Ordinal)
                ? raw[directive.Length..]
                : "";
            if (rest.StartsWith(' '))
                rest = rest[1..];
            int comment = FindUnquotedComment(rest);
            if (comment >= 0)
                rest = rest[..comment];
            return trim ? rest.Trim() : rest;
        }

        private static int FindUnquotedComment(string text)
        {
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\\' && quoted)
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                    quoted = !quoted;
                else if (ch == ';' && !quoted)
                    return i;
            }

            return -1;
        }

        private static bool HasModifier(Line line, string modifier)
        {
            for (int i = 0; i < line.Args.Count; i++)
            {
                if (!line.Args[i].Quoted && line.Args[i].Text == modifier)
                    return true;
            }

            return false;
        }

        private static string QuotedOrText(Line line, int index)
        {
            if (index >= line.Args.Count)
                return "";
            return line.Args[index].Text;
        }

        private static string TokenText(Line line, int index)
        {
            if (index >= line.Args.Count)
                return "";
            return line.Args[index].Text;
        }

        private ulong ParseUInt64(Line line, int index, string what)
        {
            string text = TokenText(line, index);
            if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
                throw Error($"invalid {what} '{text}'.");
            return value;
        }

        private uint ParseUInt32(Line line, int index, string what)
        {
            string text = TokenText(line, index);
            if (!uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
                throw Error($"invalid {what} '{text}'.");
            return value;
        }

        private byte ParseByte(Line line, int index, string what)
        {
            string text = TokenText(line, index);
            if (!byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value))
                throw Error($"invalid {what} '{text}'.");
            return value;
        }

        private static ImmutableArray<PexValue> Take(List<PexValue> values, int start, int count)
        {
            if (count <= 0)
                return ImmutableArray<PexValue>.Empty;
            var builder = ImmutableArray.CreateBuilder<PexValue>(count);
            for (int i = 0; i < count; i++)
                builder.Add(values[start + i]);
            return builder.MoveToImmutable();
        }

        private static void CountFunction(ref int functionsInObject)
        {
            functionsInObject++;
            PexLimits.Ensure(functionsInObject, PexLimits.MaxFunctionsPerObject, "functions per object");
        }

        private PexFormatException Error(string message)
        {
            int display = Math.Min(_index, _lines.Length);
            return new PexFormatException($"PAS line {display}: {message}");
        }

        private static Dictionary<string, PexOpCode> CreateOpCodeMap()
        {
            var map = new Dictionary<string, PexOpCode>(StringComparer.Ordinal);
            // Skip dialect-max aliases by *name*. `op is SkyrimMax` is a value
            // compare and would also drop ArrayRFindElement / ArrayClear /
            // ArrayGetAllMatchingStructs / TryLockGuards.
            foreach (string name in Enum.GetNames<PexOpCode>())
            {
                if (name is nameof(PexOpCode.SkyrimMax)
                    or nameof(PexOpCode.Fallout4Max)
                    or nameof(PexOpCode.Fallout76Max)
                    or nameof(PexOpCode.StarfieldMax)
                    or nameof(PexOpCode.Max))
                {
                    continue;
                }

                var op = Enum.Parse<PexOpCode>(name);
                map[PasDisassembler.OpCodeName(op)] = op;
            }

            return map;
        }

        private enum LineKind
        {
            Directive,
            Instruction,
            Label,
        }

        private readonly struct Token
        {
            public Token(string text, bool quoted)
            {
                Text = text;
                Quoted = quoted;
            }

            public string Text { get; }
            public bool Quoted { get; }
        }

        private struct Line
        {
            public LineKind Kind;
            public string Directive;
            public string Opcode;
            public string Label;
            public List<Token> Args;
            public string? Comment;
            public string Raw;
        }

        private struct RawInstruction
        {
            public PexOpCode OpCode;
            public List<Operand> Operands;
        }

        private struct Operand
        {
            public Token Token;
        }
    }
}
