using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace Papyrus.Pex;

/// <summary>
/// Thrown when a PEX blob is truncated, corrupt, or exceeds a <see cref="PexLimits"/> cap.
/// </summary>
public sealed class PexFormatException : Exception
{
    public PexFormatException(string message)
        : base(message)
    {
    }

    public PexFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Hard defensive caps. Values far beyond any vanilla or sane mod file.
/// Counts above these are <see cref="PexFormatException"/>, not a soft warning.
/// </summary>
public static class PexLimits
{
    public const int MaxStringTableEntries = 1_048_576;
    public const int MaxObjects = 4_096;
    public const int MaxFunctionsPerObject = 8_192;
    public const int MaxInstructionsPerFunction = 1_048_576;
    public const int MaxDebugInfoFunctions = 8_192;

    public static void Ensure(long count, int max, string what)
    {
        if (count > max)
            throw new PexFormatException($"{what} count {count} exceeds the limit of {max}.");
    }
}

/// <summary>
/// Bounds-checked reader for little-endian PEX 3.1–3.15 (and Skyrim big-endian files
/// whose magic is the byte-swapped form of <see cref="PexFile.MagicNumber"/>).
/// </summary>
public static class PexReader
{
    // 0xFA57C0DE laid out big-endian on disk, then read as a little-endian u32.
    internal const uint BigEndianMagicNumber = 0xDEC057FA;

    public static PexFile Read(byte[] data, GameId? preferredGame = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Read(data.AsSpan(), preferredGame);
    }

    public static PexFile Read(Stream stream, GameId? preferredGame = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        if (stream is MemoryStream memory && memory.TryGetBuffer(out ArraySegment<byte> segment))
        {
            int start = segment.Offset + (int)memory.Position;
            int count = checked((int)(memory.Length - memory.Position));
            return Read(segment.AsSpan(start, count), preferredGame);
        }

        if (stream.CanSeek)
        {
            long remaining = stream.Length - stream.Position;
            if (remaining < 0 || remaining > int.MaxValue)
                throw new PexFormatException("PEX stream length is out of range.");

            byte[] buffer = new byte[remaining];
            stream.ReadExactly(buffer);
            return Read(buffer.AsSpan(), preferredGame);
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Read(copy.ToArray().AsSpan(), preferredGame);
    }

    public static PexFile Read(ReadOnlySpan<byte> data, GameId? preferredGame = null)
    {
        var cursor = new Cursor(data);
        return cursor.ReadFile(preferredGame);
    }

    private ref struct Cursor
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;
        private bool _bigEndian;
        private ImmutableArray<string> _stringTable;
        private GameId _game;
        private bool _gameResolved;
        private bool _hasExtendedLayout;
        private bool _hasGuards;
        private PexOpCode _maxOpCode;
        private int _functionsInObject;

        public Cursor(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
            _bigEndian = false;
            _stringTable = ImmutableArray<string>.Empty;
            _game = default;
            _gameResolved = false;
            _hasExtendedLayout = false;
            _hasGuards = false;
            _maxOpCode = PexOpCode.Max;
            _functionsInObject = 0;
        }

        public PexFile ReadFile(GameId? preferredGame)
        {
            var file = new PexFile();

            uint magic = ReadUInt32LittleEndian();
            if (magic == PexFile.MagicNumber)
            {
                _bigEndian = false;
            }
            else if (magic == BigEndianMagicNumber)
            {
                // Skyrim LE/SE vanilla files are big-endian; everything else is LE.
                _bigEndian = true;
            }
            else
            {
                Fail($"invalid magic 0x{magic:X8} (expected 0x{PexFile.MagicNumber:X8}).");
            }

            file.Magic = PexFile.MagicNumber;

            byte major = ReadUInt8();
            byte minor = ReadUInt8();
            if (major != 3 || minor < 1 || minor > 15)
                Fail($"unsupported PEX version {major}.{minor}; expected 3.1–3.15.");

            var version = PexVersionInfo.FromParts(major, minor);
            file.Version = version;

            ushort rawGameId = ReadUInt16();
            file.RawGameId = rawGameId;

            GameId? resolved = GameIdMapping.Resolve(rawGameId, version, preferredGame);
            _gameResolved = resolved.HasValue;
            _game = resolved ?? default;
            file.Game = _game;

            _hasExtendedLayout = resolved is GameId.Fallout4 or GameId.Fallout76 or GameId.Starfield;
            _hasGuards = resolved == GameId.Starfield;
            _maxOpCode = _gameResolved ? GameIdMapping.GetMaxOpCode(_game) : PexOpCode.Max;

            file.CompileTime = ReadUnixTime();
            file.SourcePath = ReadPrefixedString();
            file.UserName = ReadPrefixedString();
            file.MachineName = ReadPrefixedString();

            file.StringTable = ReadStringTable();
            _stringTable = file.StringTable;

            file.DebugInfo = ReadDebugInfo();
            file.UserFlags = ReadUserFlags();
            file.Objects = ReadObjects();

            return file;
        }

        private ImmutableArray<string> ReadStringTable()
        {
            int count = ReadUInt16();
            PexLimits.Ensure(count, PexLimits.MaxStringTableEntries, "string table");

            var builder = ImmutableArray.CreateBuilder<string>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadPrefixedString());
            return builder.MoveToImmutable();
        }

        private PexDebugInfo? ReadDebugInfo()
        {
            byte hasDebug = ReadUInt8();
            if (hasDebug == 0)
                return null;

            var info = new PexDebugInfo
            {
                ModificationTime = ReadUnixTime(),
            };

            int functionCount = ReadUInt16();
            PexLimits.Ensure(functionCount, PexLimits.MaxDebugInfoFunctions, "debug-info functions");

            var functions = ImmutableArray.CreateBuilder<PexDebugFunction>(functionCount);
            for (int i = 0; i < functionCount; i++)
                functions.Add(ReadDebugFunction());
            info.Functions = functions.MoveToImmutable();

            // Skyrim (raw game id 1) has no property groups / struct-order tables.
            if (!_hasExtendedLayout)
                return info;

            int groupCount = ReadUInt16();
            var groups = ImmutableArray.CreateBuilder<PexDebugPropertyGroup>(groupCount);
            for (int i = 0; i < groupCount; i++)
                groups.Add(ReadDebugPropertyGroup());
            info.PropertyGroups = groups.MoveToImmutable();

            int orderCount = ReadUInt16();
            var orders = ImmutableArray.CreateBuilder<PexDebugStructOrder>(orderCount);
            for (int i = 0; i < orderCount; i++)
                orders.Add(ReadDebugStructOrder());
            info.StructOrders = orders.MoveToImmutable();

            return info;
        }

        private PexDebugFunction ReadDebugFunction()
        {
            var function = new PexDebugFunction
            {
                ObjectName = ReadStringIndex(),
                StateName = ReadStringIndex(),
                FunctionName = ReadStringIndex(),
                FunctionType = (PexDebugFunctionType)ReadUInt8(),
            };

            int lineCount = ReadUInt16();
            var lines = ImmutableArray.CreateBuilder<ushort>(lineCount);
            for (int i = 0; i < lineCount; i++)
                lines.Add(ReadUInt16());
            function.InstructionLineMap = lines.MoveToImmutable();
            return function;
        }

        private PexDebugPropertyGroup ReadDebugPropertyGroup()
        {
            var group = new PexDebugPropertyGroup
            {
                ObjectName = ReadStringIndex(),
                GroupName = ReadStringIndex(),
                DocumentationString = ReadStringIndex(),
                UserFlags = ReadUInt32(),
            };

            int count = ReadUInt16();
            var names = ImmutableArray.CreateBuilder<string>(count);
            for (int i = 0; i < count; i++)
                names.Add(ReadStringIndex());
            group.Properties = names.MoveToImmutable();
            return group;
        }

        private PexDebugStructOrder ReadDebugStructOrder()
        {
            var order = new PexDebugStructOrder
            {
                ObjectName = ReadStringIndex(),
                StructName = ReadStringIndex(),
            };

            int count = ReadUInt16();
            var members = ImmutableArray.CreateBuilder<string>(count);
            for (int i = 0; i < count; i++)
                members.Add(ReadStringIndex());
            order.Members = members.MoveToImmutable();
            return order;
        }

        private ImmutableArray<PexUserFlag> ReadUserFlags()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexUserFlag>(count);
            for (int i = 0; i < count; i++)
            {
                builder.Add(new PexUserFlag
                {
                    Name = ReadStringIndex(),
                    BitIndex = ReadUInt8(),
                });
            }

            return builder.MoveToImmutable();
        }

        private ImmutableArray<PexObject> ReadObjects()
        {
            int count = ReadUInt16();
            PexLimits.Ensure(count, PexLimits.MaxObjects, "objects");

            var builder = ImmutableArray.CreateBuilder<PexObject>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadObject());
            return builder.MoveToImmutable();
        }

        private PexObject ReadObject()
        {
            _functionsInObject = 0;

            var obj = new PexObject
            {
                Name = ReadStringIndex(),
            };

            uint size = ReadUInt32();
            obj.Size = size;
            if (size > (uint)Remaining)
                Fail($"object size {size} exceeds the remaining {Remaining} bytes.");

            obj.ParentClassName = ReadStringIndex();
            obj.DocumentationString = ReadStringIndex();
            if (_hasExtendedLayout)
                obj.IsConst = ReadUInt8() != 0;
            obj.UserFlags = ReadUInt32();
            obj.AutoStateName = ReadStringIndex();

            if (_hasExtendedLayout)
                obj.Structs = ReadStructs();

            obj.Variables = ReadVariables();

            if (_hasGuards)
                obj.Guards = ReadGuards();

            obj.Properties = ReadProperties();
            obj.States = ReadStates();
            return obj;
        }

        private ImmutableArray<PexStruct> ReadStructs()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexStruct>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadStruct());
            return builder.MoveToImmutable();
        }

        private PexStruct ReadStruct()
        {
            var struc = new PexStruct
            {
                Name = ReadStringIndex(),
            };

            int count = ReadUInt16();
            var members = ImmutableArray.CreateBuilder<PexStructMember>(count);
            for (int i = 0; i < count; i++)
                members.Add(ReadStructMember());
            struc.Members = members.MoveToImmutable();
            return struc;
        }

        private PexStructMember ReadStructMember()
        {
            return new PexStructMember
            {
                Name = ReadStringIndex(),
                TypeName = ReadStringIndex(),
                UserFlags = ReadUInt32(),
                DefaultValue = ReadValue(),
                IsConst = ReadUInt8() != 0,
                DocumentationString = ReadStringIndex(),
            };
        }

        private ImmutableArray<PexVariable> ReadVariables()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexVariable>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadVariable());
            return builder.MoveToImmutable();
        }

        private PexVariable ReadVariable()
        {
            var variable = new PexVariable
            {
                Name = ReadStringIndex(),
                TypeName = ReadStringIndex(),
                UserFlags = ReadUInt32(),
                DefaultValue = ReadValue(),
            };
            if (_hasExtendedLayout)
                variable.IsConst = ReadUInt8() != 0;
            return variable;
        }

        private ImmutableArray<PexGuard> ReadGuards()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexGuard>(count);
            for (int i = 0; i < count; i++)
                builder.Add(new PexGuard { Name = ReadStringIndex() });
            return builder.MoveToImmutable();
        }

        private ImmutableArray<PexProperty> ReadProperties()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexProperty>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadProperty());
            return builder.MoveToImmutable();
        }

        private PexProperty ReadProperty()
        {
            var property = new PexProperty
            {
                Name = ReadStringIndex(),
                TypeName = ReadStringIndex(),
                DocumentationString = ReadStringIndex(),
                UserFlags = ReadUInt32(),
            };

            byte flags = ReadUInt8();
            property.IsReadable = (flags & 0x01) != 0;
            property.IsWritable = (flags & 0x02) != 0;
            property.IsAuto = (flags & 0x04) != 0;

            if (property.IsAuto)
            {
                property.AutoVar = ReadStringIndex();
            }
            else
            {
                if (property.IsReadable)
                    property.ReadFunction = ReadFunction(named: false);
                if (property.IsWritable)
                    property.WriteFunction = ReadFunction(named: false);
            }

            return property;
        }

        private ImmutableArray<PexState> ReadStates()
        {
            int count = ReadUInt16();
            var builder = ImmutableArray.CreateBuilder<PexState>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadState());
            return builder.MoveToImmutable();
        }

        private PexState ReadState()
        {
            var state = new PexState
            {
                Name = ReadStringIndex(),
            };

            int count = ReadUInt16();
            AddFunctions(count);

            var functions = ImmutableArray.CreateBuilder<PexFunction>(count);
            for (int i = 0; i < count; i++)
                functions.Add(ReadFunction(named: true));
            state.Functions = functions.MoveToImmutable();
            return state;
        }

        private PexFunction ReadFunction(bool named)
        {
            if (!named)
                AddFunctions(1);

            var function = new PexFunction();
            if (named)
                function.Name = ReadStringIndex();

            function.ReturnTypeName = ReadStringIndex();
            function.DocumentationString = ReadStringIndex();
            function.UserFlags = ReadUInt32();

            byte flags = ReadUInt8();
            function.IsGlobal = (flags & 0x01) != 0;
            function.IsNative = (flags & 0x02) != 0;

            int paramCount = ReadUInt16();
            var parameters = ImmutableArray.CreateBuilder<PexFunctionParameter>(paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                parameters.Add(new PexFunctionParameter
                {
                    Name = ReadStringIndex(),
                    TypeName = ReadStringIndex(),
                });
            }

            function.Parameters = parameters.MoveToImmutable();

            int localCount = ReadUInt16();
            var locals = ImmutableArray.CreateBuilder<PexLocalVariable>(localCount);
            for (int i = 0; i < localCount; i++)
            {
                locals.Add(new PexLocalVariable
                {
                    Name = ReadStringIndex(),
                    TypeName = ReadStringIndex(),
                });
            }

            function.Locals = locals.MoveToImmutable();
            function.Instructions = ReadInstructions();
            return function;
        }

        private ImmutableArray<PexInstruction> ReadInstructions()
        {
            int count = ReadUInt16();
            PexLimits.Ensure(count, PexLimits.MaxInstructionsPerFunction, "instructions per function");

            var builder = ImmutableArray.CreateBuilder<PexInstruction>(count);
            for (int i = 0; i < count; i++)
                builder.Add(ReadInstruction());
            return builder.MoveToImmutable();
        }

        private PexInstruction ReadInstruction()
        {
            byte rawOp = ReadUInt8();
            if (rawOp > (byte)PexOpCode.Max)
                Fail($"unknown opcode 0x{rawOp:X2}.");

            var op = (PexOpCode)rawOp;
            if (op > _maxOpCode)
                Fail($"opcode {op} is not valid for this game.");

            var instruction = new PexInstruction { OpCode = op };

            int fixedCount = FixedArgumentCount(op);
            var args = ImmutableArray.CreateBuilder<PexValue>(fixedCount);
            for (int i = 0; i < fixedCount; i++)
                args.Add(ReadValue());
            instruction.Arguments = args.MoveToImmutable();

            if (HasVariadicArguments(op))
            {
                PexValue countValue = ReadValue();
                if (countValue.Type != PexValueType.Integer)
                    Fail("variadic argument count must be an integer PexValue.");

                int variadicCount = countValue.Integer;
                if (variadicCount < 0)
                    Fail($"variadic argument count {variadicCount} is negative.");
                PexLimits.Ensure(variadicCount, PexLimits.MaxInstructionsPerFunction, "variadic arguments");

                var variadic = ImmutableArray.CreateBuilder<PexValue>(variadicCount);
                for (int i = 0; i < variadicCount; i++)
                    variadic.Add(ReadValue());
                instruction.VariadicArguments = variadic.MoveToImmutable();
            }

            return instruction;
        }

        private PexValue ReadValue()
        {
            byte rawType = ReadUInt8();
            if (rawType > (byte)PexValueType.Bool)
                Fail($"unknown PexValue type 0x{rawType:X2}.");

            var type = (PexValueType)rawType;
            return type switch
            {
                PexValueType.None => PexValue.None,
                PexValueType.Identifier => new PexValue { Type = PexValueType.Identifier, Text = ReadStringIndex() },
                PexValueType.String => new PexValue { Type = PexValueType.String, Text = ReadStringIndex() },
                PexValueType.Integer => new PexValue { Type = PexValueType.Integer, Integer = (int)ReadUInt32() },
                PexValueType.Float => new PexValue { Type = PexValueType.Float, Float = ReadSingle() },
                PexValueType.Bool => new PexValue { Type = PexValueType.Bool, Bool = ReadUInt8() != 0 },
                _ => throw new PexFormatException($"PEX format error at offset {_offset}: unknown PexValue type 0x{rawType:X2}."),
            };
        }

        private void AddFunctions(int count)
        {
            PexLimits.Ensure((long)_functionsInObject + count, PexLimits.MaxFunctionsPerObject, "functions per object");
            _functionsInObject += count;
        }

        private string ReadStringIndex()
        {
            int index = ReadUInt16();
            if ((uint)index >= (uint)_stringTable.Length)
                Fail($"string table index {index} is out of range ({_stringTable.Length} entries).");
            return _stringTable[index];
        }

        private string ReadPrefixedString()
        {
            int length = ReadUInt16();
            ReadOnlySpan<byte> bytes = ReadBytes(length);
            return Encoding.Latin1.GetString(bytes);
        }

        private DateTimeOffset ReadUnixTime()
        {
            ulong seconds = ReadUInt64();
            if (seconds > (ulong)DateTimeOffset.MaxValue.ToUnixTimeSeconds())
                Fail($"unix timestamp {seconds} is out of range.");
            return DateTimeOffset.FromUnixTimeSeconds((long)seconds);
        }

        private byte ReadUInt8()
        {
            EnsureAvailable(1);
            return _data[_offset++];
        }

        private ushort ReadUInt16()
        {
            EnsureAvailable(2);
            ushort value = _bigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(_offset, 2))
                : BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_offset, 2));
            _offset += 2;
            return value;
        }

        private uint ReadUInt32()
        {
            EnsureAvailable(4);
            uint value = _bigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(_offset, 4))
                : BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_offset, 4));
            _offset += 4;
            return value;
        }

        private uint ReadUInt32LittleEndian()
        {
            EnsureAvailable(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_offset, 4));
            _offset += 4;
            return value;
        }

        private ulong ReadUInt64()
        {
            EnsureAvailable(8);
            ulong value = _bigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(_offset, 8))
                : BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_offset, 8));
            _offset += 8;
            return value;
        }

        private float ReadSingle()
        {
            EnsureAvailable(4);
            float value = _bigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(_data.Slice(_offset, 4))
                : BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(_offset, 4));
            _offset += 4;
            return value;
        }

        private ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (length < 0)
                Fail($"length-prefixed blob size {length} is negative.");
            if (length > Remaining)
                Fail($"length-prefixed blob of {length} bytes exceeds the remaining {Remaining} bytes.");

            ReadOnlySpan<byte> slice = _data.Slice(_offset, length);
            _offset += length;
            return slice;
        }

        private int Remaining => _data.Length - _offset;

        private void EnsureAvailable(int byteCount)
        {
            if (byteCount > Remaining)
                Fail($"expected {byteCount} more bytes, {Remaining} remaining.");
        }

        private void Fail(string message)
        {
            throw new PexFormatException($"PEX format error at offset {_offset}: {message}");
        }

        // Fixed operand counts from Caprica PexInstruction.h / PexFunctionBuilder.h OPCODES.
        private static int FixedArgumentCount(PexOpCode op)
        {
            switch (op)
            {
                case PexOpCode.Nop:
                case PexOpCode.LockGuards:
                case PexOpCode.UnlockGuards:
                    return 0;
                case PexOpCode.Jmp:
                case PexOpCode.Return:
                case PexOpCode.StructCreate:
                case PexOpCode.ArrayRemoveLast:
                case PexOpCode.ArrayClear:
                case PexOpCode.TryLockGuards:
                    return 1;
                case PexOpCode.Not:
                case PexOpCode.INeg:
                case PexOpCode.FNeg:
                case PexOpCode.Assign:
                case PexOpCode.Cast:
                case PexOpCode.JmpT:
                case PexOpCode.JmpF:
                case PexOpCode.CallParent:
                case PexOpCode.ArrayCreate:
                case PexOpCode.ArrayLength:
                    return 2;
                case PexOpCode.IAdd:
                case PexOpCode.FAdd:
                case PexOpCode.ISub:
                case PexOpCode.FSub:
                case PexOpCode.IMul:
                case PexOpCode.FMul:
                case PexOpCode.IDiv:
                case PexOpCode.FDiv:
                case PexOpCode.IMod:
                case PexOpCode.CmpEq:
                case PexOpCode.CmpLt:
                case PexOpCode.CmpLte:
                case PexOpCode.CmpGt:
                case PexOpCode.CmpGte:
                case PexOpCode.CallMethod:
                case PexOpCode.CallStatic:
                case PexOpCode.StrCat:
                case PexOpCode.PropGet:
                case PexOpCode.PropSet:
                case PexOpCode.ArrayGetElement:
                case PexOpCode.ArraySetElement:
                case PexOpCode.Is:
                case PexOpCode.StructGet:
                case PexOpCode.StructSet:
                case PexOpCode.ArrayAdd:
                case PexOpCode.ArrayInsert:
                case PexOpCode.ArrayRemove:
                    return 3;
                case PexOpCode.ArrayFindElement:
                case PexOpCode.ArrayRFindElement:
                    return 4;
                case PexOpCode.ArrayFindStruct:
                case PexOpCode.ArrayRFindStruct:
                    return 5;
                case PexOpCode.ArrayGetAllMatchingStructs:
                    return 6;
                default:
                    throw new PexFormatException($"Unknown opcode {op}.");
            }
        }

        private static bool HasVariadicArguments(PexOpCode op)
        {
            return op is PexOpCode.CallMethod
                or PexOpCode.CallParent
                or PexOpCode.CallStatic
                or PexOpCode.LockGuards
                or PexOpCode.UnlockGuards
                or PexOpCode.TryLockGuards;
        }
    }
}
