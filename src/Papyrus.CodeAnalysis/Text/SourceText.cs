using System;

namespace Papyrus.CodeAnalysis.Text;

public abstract class SourceText
{
    private protected SourceText(PapyrusEncoding encoding)
    {
        Encoding = encoding;
    }

    public PapyrusEncoding Encoding { get; }

    public abstract int Length { get; }

    public abstract char GetChar(int position);

    public static SourceText From(string text, PapyrusEncoding encoding = PapyrusEncoding.Windows1252)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return new StringSourceText(text, encoding);
    }

    public static SourceText From(byte[] buffer, PapyrusEncoding encoding = PapyrusEncoding.Windows1252)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return From(buffer.AsSpan(), encoding);
    }

    public static SourceText From(ReadOnlySpan<byte> buffer, PapyrusEncoding encoding = PapyrusEncoding.Windows1252)
    {
        var detected = encoding;
        if (HasUtf8Bom(buffer))
        {
            detected = PapyrusEncoding.Utf8;
        }

        var decoded = PapyrusEncodings.GetEncoding(detected).GetString(buffer);
        return new StringSourceText(decoded, detected);
    }

    public override string ToString() => ToString(new TextSpan(0, Length));

    public abstract string ToString(TextSpan span);

    private static bool HasUtf8Bom(ReadOnlySpan<byte> buffer)
    {
        return buffer.Length >= 3
            && buffer[0] == 0xEF
            && buffer[1] == 0xBB
            && buffer[2] == 0xBF;
    }
}

internal sealed class StringSourceText : SourceText
{
    private readonly string _text;

    internal StringSourceText(string text, PapyrusEncoding encoding)
        : base(encoding)
    {
        _text = text;
    }

    public override int Length => _text.Length;

    public override char GetChar(int position)
    {
        if ((uint)position >= (uint)_text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return _text[position];
    }

    public override string ToString(TextSpan span)
    {
        if (span.Start > _text.Length || span.End > _text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(span));
        }

        return _text.Substring(span.Start, span.Length);
    }
}
