using System;
using System.Collections.Immutable;

namespace Papyrus.Pex;

public sealed class PexFile
{
    public const uint MagicNumber = 0xFA57C0DE;

    public uint Magic { get; set; } = MagicNumber;
    public PexVersion Version { get; set; }
    public ushort RawGameId { get; set; }
    public GameId Game { get; set; }
    public DateTimeOffset CompileTime { get; set; }
    public string SourcePath { get; set; } = "";
    public string UserName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public ImmutableArray<string> StringTable { get; set; } = ImmutableArray<string>.Empty;
    public PexDebugInfo? DebugInfo { get; set; }
    public ImmutableArray<PexUserFlag> UserFlags { get; set; } = ImmutableArray<PexUserFlag>.Empty;
    public ImmutableArray<PexObject> Objects { get; set; } = ImmutableArray<PexObject>.Empty;
}

public sealed class PexUserFlag
{
    public string Name { get; set; } = "";
    public byte BitIndex { get; set; }
}
