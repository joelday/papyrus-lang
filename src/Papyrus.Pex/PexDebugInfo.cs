using System;
using System.Collections.Immutable;

namespace Papyrus.Pex;

public enum PexDebugFunctionType : byte
{
    Normal = 0,
    Getter = 1,
    Setter = 2,
}

public sealed class PexDebugInfo
{
    public DateTimeOffset ModificationTime { get; set; }
    public ImmutableArray<PexDebugFunction> Functions { get; set; } = ImmutableArray<PexDebugFunction>.Empty;
    public ImmutableArray<PexDebugPropertyGroup> PropertyGroups { get; set; } = ImmutableArray<PexDebugPropertyGroup>.Empty;
    public ImmutableArray<PexDebugStructOrder> StructOrders { get; set; } = ImmutableArray<PexDebugStructOrder>.Empty;
}

public sealed class PexDebugFunction
{
    public string ObjectName { get; set; } = "";
    public string StateName { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public PexDebugFunctionType FunctionType { get; set; }
    public ImmutableArray<ushort> InstructionLineMap { get; set; } = ImmutableArray<ushort>.Empty;
}

public sealed class PexDebugPropertyGroup
{
    public string ObjectName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string DocumentationString { get; set; } = "";
    public uint UserFlags { get; set; }
    public ImmutableArray<string> Properties { get; set; } = ImmutableArray<string>.Empty;
}

public sealed class PexDebugStructOrder
{
    public string ObjectName { get; set; } = "";
    public string StructName { get; set; } = "";
    public ImmutableArray<string> Members { get; set; } = ImmutableArray<string>.Empty;
}
