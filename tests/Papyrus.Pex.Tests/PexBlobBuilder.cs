using System;
using System.IO;
using System.Text;

namespace Papyrus.Pex.Tests;

/// <summary>
/// Tiny little-endian writer for synthetic PEX fixtures. Not the P0.04 PexWriter.
/// </summary>
internal sealed class PexBlobBuilder
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;

    public PexBlobBuilder()
    {
        _writer = new BinaryWriter(_stream, Encoding.Latin1, leaveOpen: true);
    }

    public PexBlobBuilder U8(byte value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder U16(ushort value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder U32(uint value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder I32(int value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder U64(ulong value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder F32(float value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder Bytes(params byte[] value)
    {
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder Str(string value)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
            throw new InvalidOperationException("String exceeds u16 length prefix.");
        _writer.Write((ushort)bytes.Length);
        _writer.Write(bytes);
        return this;
    }

    public PexBlobBuilder StrBytes(params byte[] value)
    {
        if (value.Length > ushort.MaxValue)
            throw new InvalidOperationException("String exceeds u16 length prefix.");
        _writer.Write((ushort)value.Length);
        _writer.Write(value);
        return this;
    }

    public PexBlobBuilder MagicLe()
    {
        _writer.Write(PexFile.MagicNumber);
        return this;
    }

    public PexBlobBuilder ValueNone() => U8((byte)PexValueType.None);

    public PexBlobBuilder ValueIdent(ushort stringIndex) =>
        U8((byte)PexValueType.Identifier).U16(stringIndex);

    public PexBlobBuilder ValueString(ushort stringIndex) =>
        U8((byte)PexValueType.String).U16(stringIndex);

    public PexBlobBuilder ValueInt(int value) =>
        U8((byte)PexValueType.Integer).I32(value);

    public PexBlobBuilder ValueFloat(float value) =>
        U8((byte)PexValueType.Float).F32(value);

    public PexBlobBuilder ValueBool(bool value) =>
        U8((byte)PexValueType.Bool).U8(value ? (byte)1 : (byte)0);

    public PexBlobBuilder Version(PexVersion version) =>
        U8(PexVersionInfo.GetMajor(version)).U8(PexVersionInfo.GetMinor(version));

    /// <summary>
    /// Header through machine name. Caller writes string table, debug, flags, objects.
    /// </summary>
    public PexBlobBuilder Header(
        PexVersion version,
        ushort rawGameId,
        ulong compileTime = 0,
        string source = "Test.psc",
        string user = "user",
        string machine = "host")
    {
        return MagicLe()
            .Version(version)
            .U16(rawGameId)
            .U64(compileTime)
            .Str(source)
            .Str(user)
            .Str(machine);
    }

    public PexBlobBuilder EmptyTail()
    {
        return U16(0) // string table
            .U8(0)    // no debug
            .U16(0)   // user flags
            .U16(0);  // objects
    }

    public byte[] ToArray()
    {
        _writer.Flush();
        return _stream.ToArray();
    }
}
