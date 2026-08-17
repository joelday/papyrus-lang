namespace Papyrus.Pex;

public enum PexVersion : ushort
{
    V31 = 0x0301,       // Skyrim LE vanilla base game
    V32 = 0x0302,       // Skyrim LE expansions, SE, AE
    V39 = 0x0309,       // Fallout 4
    V312 = 0x030C,      // Starfield
    V315 = 0x030F,      // Fallout 76 (read only)
}

public static class PexVersionInfo
{
    public static byte GetMajor(PexVersion version) => (byte)((ushort)version >> 8);

    public static byte GetMinor(PexVersion version) => (byte)((ushort)version & 0xFF);

    public static PexVersion FromParts(byte major, byte minor) => (PexVersion)((major << 8) | minor);
}
