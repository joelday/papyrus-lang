using System.Collections.Immutable;

namespace Papyrus.Pex;

public enum PexValueType : byte
{
    None = 0,
    Identifier = 1,
    String = 2,
    Integer = 3,
    Float = 4,
    Bool = 5,
}

public readonly struct PexValue
{
    public static PexValue None => default;

    public PexValueType Type { get; init; }
    public string? Text { get; init; }
    public int Integer { get; init; }
    public float Float { get; init; }
    public bool Bool { get; init; }
}

public sealed class PexInstruction
{
    public PexOpCode OpCode { get; set; }
    public ImmutableArray<PexValue> Arguments { get; set; } = ImmutableArray<PexValue>.Empty;
    public ImmutableArray<PexValue> VariadicArguments { get; set; } = ImmutableArray<PexValue>.Empty;
}
