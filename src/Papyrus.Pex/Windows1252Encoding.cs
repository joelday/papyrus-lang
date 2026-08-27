using System;
using System.Collections.Generic;
using System.Text;

namespace Papyrus.Pex;

/// <summary>
/// PEX length-prefixed strings are Windows-1252 (K19).
/// </summary>
public static class PexEncoding
{
    public static Encoding Windows1252 => Windows1252Encoding.Instance;
}

/// <summary>
/// Hand-rolled Windows-1252 for PEX strings and source decode.
/// Avoids <see cref="Encoding.GetEncoding(int)"/> (code pages / NativeAOT).
/// </summary>
internal sealed class Windows1252Encoding : Encoding
{
    internal static readonly Windows1252Encoding Instance = new();

    // Bytes 0x80-0x9F. Undefined slots keep the C1 control so round-trip of those bytes is possible.
    private static readonly char[] ByteToChar =
    {
        '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
        '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
    };

    private static readonly Dictionary<char, byte> CharToByte = CreateCharToByte();

    public override string EncodingName => "Western European (Windows)";

    public override string WebName => "windows-1252";

    public override int CodePage => 1252;

    public override bool IsSingleByte => true;

    public override int GetMaxByteCount(int charCount)
    {
        if (charCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charCount));
        }

        return charCount;
    }

    public override int GetMaxCharCount(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        return byteCount;
    }

    public override int GetByteCount(char[] chars, int index, int count)
    {
        ValidateRead(chars, index, count);
        return count;
    }

    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        ValidateRead(bytes, index, count);
        return count;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        ValidateRead(chars, charIndex, charCount);
        ValidateWrite(bytes, byteIndex, charCount);

        for (var i = 0; i < charCount; i++)
        {
            bytes[byteIndex + i] = Encode(chars[charIndex + i]);
        }

        return charCount;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        ValidateRead(bytes, byteIndex, byteCount);
        ValidateWrite(chars, charIndex, byteCount);

        for (var i = 0; i < byteCount; i++)
        {
            chars[charIndex + i] = Decode(bytes[byteIndex + i]);
        }

        return byteCount;
    }

    public override byte[] GetPreamble() => Array.Empty<byte>();

    internal static char Decode(byte value)
    {
        if (value < 0x80 || value >= 0xA0)
        {
            return (char)value;
        }

        return ByteToChar[value - 0x80];
    }

    private static byte Encode(char value)
    {
        if (value < 0x80 || (value >= 0xA0 && value <= 0xFF))
        {
            return (byte)value;
        }

        if (CharToByte.TryGetValue(value, out var mapped))
        {
            return mapped;
        }

        return (byte)'?';
    }

    private static Dictionary<char, byte> CreateCharToByte()
    {
        var map = new Dictionary<char, byte>(ByteToChar.Length);
        for (var i = 0; i < ByteToChar.Length; i++)
        {
            map[ByteToChar[i]] = (byte)(0x80 + i);
        }

        return map;
    }

    private static void ValidateRead<T>(T[] buffer, int index, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if ((uint)index > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if ((uint)count > (uint)(buffer.Length - index))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    private static void ValidateWrite<T>(T[] buffer, int index, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if ((uint)index > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (count > buffer.Length - index)
        {
            throw new ArgumentException("Destination is too small.", nameof(buffer));
        }
    }
}
