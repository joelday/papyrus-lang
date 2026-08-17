using System.Collections.Immutable;

namespace Papyrus.Pex;

/// <summary>
/// PEX-owned reflection DTO. Strings only — not <c>IScriptSymbol</c>.
/// </summary>
public sealed class PexScriptMetadata
{
    public PexQualifiedName Name { get; init; }
    public PexQualifiedName? BaseType { get; init; }
    public ImmutableArray<PexFunctionMetadata> Functions { get; init; } = ImmutableArray<PexFunctionMetadata>.Empty;
    public ImmutableArray<PexPropertyMetadata> Properties { get; init; } = ImmutableArray<PexPropertyMetadata>.Empty;
    public ImmutableArray<PexStructMetadata> Structs { get; init; } = ImmutableArray<PexStructMetadata>.Empty;
    public ImmutableArray<PexGuardMetadata> Guards { get; init; } = ImmutableArray<PexGuardMetadata>.Empty;
    public ImmutableArray<PexStateMetadata> States { get; init; } = ImmutableArray<PexStateMetadata>.Empty;
    /// <summary>Object user-flag mask. Bits are named by <see cref="UserFlagNames"/>[<c>bit</c>].</summary>
    public uint UserFlags { get; init; }
    /// <summary>
    /// 32 slots indexed by <c>PexUserFlag.BitIndex</c>, or empty when the file
    /// defines no flags. File order is not significant.
    /// </summary>
    public ImmutableArray<string> UserFlagNames { get; init; } = ImmutableArray<string>.Empty;
}

public sealed class PexFunctionMetadata
{
    public string Name { get; init; } = "";
    public string ReturnTypeName { get; init; } = "";
    public uint UserFlags { get; init; }
    public bool IsNative { get; init; }
    public bool IsGlobal { get; init; }
    public ImmutableArray<PexParameterMetadata> Parameters { get; init; } = ImmutableArray<PexParameterMetadata>.Empty;
}

public sealed class PexParameterMetadata
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
}

public sealed class PexPropertyMetadata
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public uint UserFlags { get; init; }
    public bool IsAuto { get; init; }
    public bool IsReadable { get; init; }
    public bool IsWritable { get; init; }
    public PexFunctionMetadata? GetMethod { get; init; }
    public PexFunctionMetadata? SetMethod { get; init; }
}

public sealed class PexStructMetadata
{
    public string Name { get; init; } = "";
    public ImmutableArray<PexStructMemberMetadata> Members { get; init; } = ImmutableArray<PexStructMemberMetadata>.Empty;
}

public sealed class PexStructMemberMetadata
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public uint UserFlags { get; init; }
    public bool IsConst { get; init; }
}

public sealed class PexGuardMetadata
{
    public string Name { get; init; } = "";
}

public sealed class PexStateMetadata
{
    public string Name { get; init; } = "";
    public bool IsAuto { get; init; }
    public ImmutableArray<PexFunctionMetadata> Functions { get; init; } = ImmutableArray<PexFunctionMetadata>.Empty;
}
