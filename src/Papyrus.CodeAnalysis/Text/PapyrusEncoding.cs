using System.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Text;

public enum PapyrusEncoding
{
    Windows1252 = 0,
    Utf8 = 1,
}

public static class PapyrusEncodings
{
    internal static readonly Encoding Windows1252 = Windows1252Encoding.Instance;

    internal static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: true,
        throwOnInvalidBytes: false);

    public static Encoding GetEncoding(PapyrusEncoding encoding)
    {
        return encoding == PapyrusEncoding.Utf8 ? Utf8 : Windows1252;
    }
}
