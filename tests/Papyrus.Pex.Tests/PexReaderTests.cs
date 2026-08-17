using System;
using System.IO;
using Xunit;

namespace Papyrus.Pex.Tests;

public sealed class PexReaderTests
{
    [Fact]
    public void Read_MinimalSkyrim_ParsesHeader()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw, compileTime: 1_700_000_000)
            .EmptyTail()
            .ToArray();

        PexFile file = PexReader.Read(bytes);

        Assert.Equal(PexFile.MagicNumber, file.Magic);
        Assert.Equal(PexVersion.V32, file.Version);
        Assert.Equal(GameIdMapping.SkyrimRaw, file.RawGameId);
        Assert.Equal(GameId.SkyrimSpecialEdition, file.Game);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), file.CompileTime);
        Assert.Equal("Test.psc", file.SourcePath);
        Assert.Equal("user", file.UserName);
        Assert.Equal("host", file.MachineName);
        Assert.Empty(file.StringTable);
        Assert.Null(file.DebugInfo);
        Assert.Empty(file.UserFlags);
        Assert.Empty(file.Objects);
    }

    [Fact]
    public void Read_SkyrimV31_ResolvesLegacySkyrim()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V31, GameIdMapping.SkyrimRaw)
            .EmptyTail()
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Equal(GameId.Skyrim, file.Game);
    }

    [Fact]
    public void Read_SkyrimV32_PreferredGameForcesLegacy()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .EmptyTail()
            .ToArray();

        PexFile file = PexReader.Read(bytes, GameId.Skyrim);
        Assert.Equal(GameId.Skyrim, file.Game);
    }

    [Fact]
    public void Read_UnknownGameId_LeavesGameDefault()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, rawGameId: 99)
            .EmptyTail()
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Equal((ushort)99, file.RawGameId);
        Assert.Equal(default, file.Game);
    }

    [Fact]
    public void Read_MinimalObjectWithNop_IsRoundTripReady()
    {
        byte[] bytes = BuildSkyrimScriptWithNop();

        PexFile file = PexReader.Read(bytes);

        Assert.Equal(new[] { "MyScript", "", "docs", "Int", "MyFunc", "None", "::temp", "Hidden" }, file.StringTable);
        Assert.Single(file.UserFlags);
        Assert.Equal("Hidden", file.UserFlags[0].Name);
        Assert.Equal((byte)0, file.UserFlags[0].BitIndex);

        PexDebugInfo debug = Assert.IsType<PexDebugInfo>(file.DebugInfo);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(42), debug.ModificationTime);
        Assert.Single(debug.Functions);
        Assert.Equal("MyScript", debug.Functions[0].ObjectName);
        Assert.Equal("", debug.Functions[0].StateName);
        Assert.Equal("MyFunc", debug.Functions[0].FunctionName);
        Assert.Equal(PexDebugFunctionType.Normal, debug.Functions[0].FunctionType);
        Assert.Equal(new ushort[] { 10 }, debug.Functions[0].InstructionLineMap);
        Assert.Empty(debug.PropertyGroups);
        Assert.Empty(debug.StructOrders);

        Assert.Single(file.Objects);
        PexObject obj = file.Objects[0];
        Assert.Equal("MyScript", obj.Name);
        Assert.Equal((uint)0, obj.Size);
        Assert.Equal("", obj.ParentClassName);
        Assert.Equal("docs", obj.DocumentationString);
        Assert.False(obj.IsConst);
        Assert.Equal(1u, obj.UserFlags);
        Assert.Equal("", obj.AutoStateName);
        Assert.Empty(obj.Structs);
        Assert.Single(obj.Variables);
        Assert.Equal("::temp", obj.Variables[0].Name);
        Assert.Equal("Int", obj.Variables[0].TypeName);
        Assert.Equal(PexValueType.Integer, obj.Variables[0].DefaultValue.Type);
        Assert.Equal(7, obj.Variables[0].DefaultValue.Integer);
        Assert.Empty(obj.Guards);
        Assert.Empty(obj.Properties);

        Assert.Single(obj.States);
        PexState state = obj.States[0];
        Assert.Equal("", state.Name);
        Assert.Single(state.Functions);

        PexFunction function = state.Functions[0];
        Assert.Equal("MyFunc", function.Name);
        Assert.Equal("None", function.ReturnTypeName);
        Assert.Equal("", function.DocumentationString);
        Assert.False(function.IsGlobal);
        Assert.False(function.IsNative);
        Assert.Empty(function.Parameters);
        Assert.Empty(function.Locals);
        Assert.Single(function.Instructions);
        Assert.Equal(PexOpCode.Nop, function.Instructions[0].OpCode);
        Assert.Empty(function.Instructions[0].Arguments);
    }

    [Fact]
    public void Read_Fallout4_ReadsConstAndStructs()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V39, GameIdMapping.Fallout4Raw)
            .U16(5)
            .Str("Foo")
            .Str("Bar")
            .Str("")
            .Str("Int")
            .Str("Member")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0) // object name Foo
            .U32(0)
            .U16(2) // parent
            .U16(2) // doc
            .U8(1)  // const
            .U32(0)
            .U16(2) // auto state
            .U16(1) // one struct
            .U16(1) // Bar
            .U16(1) // one member
            .U16(4) // Member
            .U16(3) // Int
            .U32(0)
            .ValueInt(3)
            .U8(1)
            .U16(2)
            .U16(0) // vars
            .U16(0) // props
            .U16(0) // states
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Equal(GameId.Fallout4, file.Game);
        PexObject obj = Assert.Single(file.Objects);
        Assert.True(obj.IsConst);
        PexStruct struc = Assert.Single(obj.Structs);
        Assert.Equal("Bar", struc.Name);
        PexStructMember member = Assert.Single(struc.Members);
        Assert.Equal("Member", member.Name);
        Assert.Equal("Int", member.TypeName);
        Assert.True(member.IsConst);
        Assert.Equal(3, member.DefaultValue.Integer);
    }

    [Fact]
    public void Read_Fallout76_AllowsArrayGetAllMatchingStructs()
    {
        byte[] bytes = BuildInstructionFile(
            PexVersion.V315,
            GameIdMapping.Fallout76Raw,
            extended: true,
            opcode: PexOpCode.ArrayGetAllMatchingStructs,
            args: b => b
                .ValueIdent(3)
                .ValueIdent(3)
                .ValueString(2)
                .ValueInt(0)
                .ValueInt(0)
                .ValueInt(1));

        PexFile file = PexReader.Read(bytes);
        Assert.Equal(GameId.Fallout76, file.Game);
        PexInstruction inst = file.Objects[0].States[0].Functions[0].Instructions[0];
        Assert.Equal(PexOpCode.ArrayGetAllMatchingStructs, inst.OpCode);
        Assert.Equal(6, inst.Arguments.Length);
    }

    [Fact]
    public void Read_Starfield_ReadsGuardsAndLockOpcode()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V312, GameIdMapping.StarfieldRaw)
            .U16(3)
            .Str("Ship")
            .Str("")
            .Str("MyGuard")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U8(0)
            .U32(0)
            .U16(1)
            .U16(0) // structs
            .U16(0) // vars
            .U16(1) // one guard
            .U16(2)
            .U16(0) // props
            .U16(1) // one state
            .U16(1)
            .U16(1) // one function
            .U16(2) // name reused
            .U16(1) // return None-ish empty
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)PexOpCode.LockGuards)
            .ValueInt(0)
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Equal(GameId.Starfield, file.Game);
        PexObject obj = Assert.Single(file.Objects);
        Assert.Equal("MyGuard", Assert.Single(obj.Guards).Name);
        Assert.Equal(PexOpCode.LockGuards, obj.States[0].Functions[0].Instructions[0].OpCode);
    }

    [Fact]
    public void Read_CallMethod_ReadsVariadicArguments()
    {
        byte[] bytes = BuildInstructionFile(
            PexVersion.V32,
            GameIdMapping.SkyrimRaw,
            extended: false,
            opcode: PexOpCode.CallMethod,
            args: b => b
                .ValueString(2)  // function name
                .ValueIdent(0)   // base
                .ValueIdent(3)   // dest
                .ValueInt(2)     // variadic count
                .ValueInt(1)
                .ValueIdent(3));

        PexFile file = PexReader.Read(bytes);
        PexInstruction inst = file.Objects[0].States[0].Functions[0].Instructions[0];
        Assert.Equal(PexOpCode.CallMethod, inst.OpCode);
        Assert.Equal(3, inst.Arguments.Length);
        Assert.Equal(2, inst.VariadicArguments.Length);
        Assert.Equal(1, inst.VariadicArguments[0].Integer);
        Assert.Equal("::dest", inst.VariadicArguments[1].Text);
    }

    [Fact]
    public void Read_IAdd_ReadsTypedOperands()
    {
        byte[] bytes = BuildInstructionFile(
            PexVersion.V32,
            GameIdMapping.SkyrimRaw,
            extended: false,
            opcode: PexOpCode.IAdd,
            args: b => b.ValueIdent(3).ValueInt(1).ValueInt(2));

        PexFile file = PexReader.Read(bytes);
        PexInstruction inst = file.Objects[0].States[0].Functions[0].Instructions[0];
        Assert.Equal(PexOpCode.IAdd, inst.OpCode);
        Assert.Equal("::dest", inst.Arguments[0].Text);
        Assert.Equal(PexValueType.Identifier, inst.Arguments[0].Type);
        Assert.Equal(1, inst.Arguments[1].Integer);
        Assert.Equal(2, inst.Arguments[2].Integer);
    }

    [Fact]
    public void Read_PropertyWithGetter_CountsAsFunction()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(5)
            .Str("Script")
            .Str("")
            .Str("Prop")
            .Str("Int")
            .Str("None")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1)
            .U32(0)
            .U16(1)
            .U16(0) // vars
            .U16(1) // one property
            .U16(2)
            .U16(3)
            .U16(1)
            .U32(0)
            .U8(0x01) // readable, not auto
            .U16(4)   // return None
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)PexOpCode.Return)
            .ValueNone()
            .U16(0) // states
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        PexProperty prop = Assert.Single(file.Objects[0].Properties);
        Assert.True(prop.IsReadable);
        Assert.False(prop.IsAuto);
        Assert.NotNull(prop.ReadFunction);
        Assert.Equal("None", prop.ReadFunction!.ReturnTypeName);
        Assert.Equal(PexOpCode.Return, prop.ReadFunction.Instructions[0].OpCode);
    }

    [Fact]
    public void Read_StreamAndSpan_Match()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .EmptyTail()
            .ToArray();

        PexFile fromArray = PexReader.Read(bytes);
        PexFile fromSpan = PexReader.Read(bytes.AsSpan());
        using var stream = new MemoryStream(bytes);
        PexFile fromStream = PexReader.Read(stream);

        Assert.Equal(fromArray.SourcePath, fromSpan.SourcePath);
        Assert.Equal(fromArray.SourcePath, fromStream.SourcePath);
        Assert.Equal(fromArray.Game, fromStream.Game);
    }

    [Fact]
    public void Read_NonSeekableStream_Parses()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .EmptyTail()
            .ToArray();

        using var inner = new MemoryStream(bytes);
        using var stream = new NonSeekableStream(inner);
        PexFile file = PexReader.Read(stream);
        Assert.Equal("Test.psc", file.SourcePath);
    }

    [Fact]
    public void Read_BigEndianMagic_IsAccepted()
    {
        // BE 0xFA57C0DE on disk is bytes FA 57 C0 DE.
        var builder = new PexBlobBuilder()
            .Bytes(0xFA, 0x57, 0xC0, 0xDE)
            .U8(3).U8(1);
        // Remaining multi-byte fields are big-endian.
        builder.Bytes(0x00, 0x01); // gameId 1
        builder.Bytes(0, 0, 0, 0, 0, 0, 0, 0); // time
        builder.Bytes(0x00, 0x01).Bytes((byte)'A'); // source "A"
        builder.Bytes(0x00, 0x01).Bytes((byte)'B');
        builder.Bytes(0x00, 0x01).Bytes((byte)'C');
        builder.Bytes(0x00, 0x00); // strings
        builder.U8(0);
        builder.Bytes(0x00, 0x00);
        builder.Bytes(0x00, 0x00);

        PexFile file = PexReader.Read(builder.ToArray());
        Assert.Equal(PexVersion.V31, file.Version);
        Assert.Equal(GameId.Skyrim, file.Game);
        Assert.Equal("A", file.SourcePath);
        Assert.Equal("B", file.UserName);
        Assert.Equal("C", file.MachineName);
    }

    [Fact]
    public void Read_BadMagic_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .U32(0xDEADBEEF)
            .U8(3).U8(2)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("magic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_UnsupportedVersion_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .MagicLe()
            .U8(4).U8(0)
            .U16(1)
            .U64(0)
            .Str("a").Str("b").Str("c")
            .EmptyTail()
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_TruncatedHeader_Throws()
    {
        byte[] bytes = [0xDE, 0xC0, 0x57];
        Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
    }

    [Fact]
    public void Read_TruncatedStringTableEntry_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(1)
            .U16(8) // claims 8 bytes
            .Bytes(1, 2, 3) // only 3 present
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("exceeds the remaining", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_OversizeBlob_Throws()
    {
        // Source-name length prefix larger than the remaining stream.
        byte[] bytes = new PexBlobBuilder()
            .MagicLe()
            .Version(PexVersion.V32)
            .U16(GameIdMapping.SkyrimRaw)
            .U64(0)
            .U16(40000)
            .Bytes(1, 2)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("exceeds the remaining", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_OversizeObject_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(1)
            .Str("X")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(uint.MaxValue)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("object size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_StringIndexOutOfRange_Throws()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(1)
            .Str("Only")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(5) // index 5, table has 1 entry
            .U32(0)
            .ToArray();

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("string table index", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_SkyrimFile_RejectsFalloutOpcode()
    {
        byte[] bytes = BuildInstructionFile(
            PexVersion.V32,
            GameIdMapping.SkyrimRaw,
            extended: false,
            opcode: PexOpCode.Is,
            args: b => b.ValueIdent(3).ValueIdent(0).ValueIdent(0));

        PexFormatException ex = Assert.Throws<PexFormatException>(() => PexReader.Read(bytes));
        Assert.Contains("not valid for this game", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_DoesNotUseWallClock()
    {
        byte[] bytes = new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw, compileTime: 0)
            .EmptyTail()
            .ToArray();

        PexFile file = PexReader.Read(bytes);
        Assert.Equal(DateTimeOffset.UnixEpoch, file.CompileTime);
    }

    [Fact]
    public void Read_NullArray_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PexReader.Read((byte[])null!));
    }

    [Fact]
    public void Read_NullStream_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PexReader.Read((Stream)null!));
    }

    private static byte[] BuildSkyrimScriptWithNop()
    {
        return new PexBlobBuilder()
            .Header(PexVersion.V32, GameIdMapping.SkyrimRaw)
            .U16(8)
            .Str("MyScript")
            .Str("")
            .Str("docs")
            .Str("Int")
            .Str("MyFunc")
            .Str("None")
            .Str("::temp")
            .Str("Hidden")
            .U8(1) // has debug
            .U64(42)
            .U16(1)
            .U16(0) // object
            .U16(1) // state
            .U16(4) // function
            .U8((byte)PexDebugFunctionType.Normal)
            .U16(1)
            .U16(10)
            .U16(1) // one user flag
            .U16(7)
            .U8(0)
            .U16(1) // one object
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(2)
            .U32(1)
            .U16(1)
            .U16(1) // one variable
            .U16(6)
            .U16(3)
            .U32(0)
            .ValueInt(7)
            .U16(0) // properties
            .U16(1) // one state
            .U16(1)
            .U16(1) // one function
            .U16(4)
            .U16(5)
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)PexOpCode.Nop)
            .ToArray();
    }

    private static byte[] BuildInstructionFile(
        PexVersion version,
        ushort rawGameId,
        bool extended,
        PexOpCode opcode,
        Action<PexBlobBuilder> args)
    {
        var b = new PexBlobBuilder()
            .Header(version, rawGameId)
            .U16(4)
            .Str("Script")
            .Str("")
            .Str("Fn")
            .Str("::dest")
            .U8(0)
            .U16(0)
            .U16(1)
            .U16(0)
            .U32(0)
            .U16(1)
            .U16(1);
        if (extended)
            b.U8(0);
        b.U32(0).U16(1);
        if (extended)
            b.U16(0); // structs
        b.U16(0); // vars
        if (rawGameId == GameIdMapping.StarfieldRaw)
            b.U16(0); // guards
        b.U16(0) // props
            .U16(1)
            .U16(1)
            .U16(1)
            .U16(2)
            .U16(1)
            .U16(1)
            .U32(0)
            .U8(0)
            .U16(0)
            .U16(0)
            .U16(1)
            .U8((byte)opcode);
        args(b);
        return b.ToArray();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
