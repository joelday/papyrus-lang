using System.Collections.Immutable;

namespace Papyrus.Pex;

public sealed class PexObject
{
    public string Name { get; set; } = "";
    public uint Size { get; set; }
    public string ParentClassName { get; set; } = "";
    public string DocumentationString { get; set; } = "";
    public bool IsConst { get; set; }
    public uint UserFlags { get; set; }
    public string AutoStateName { get; set; } = "";
    public ImmutableArray<PexStruct> Structs { get; set; } = ImmutableArray<PexStruct>.Empty;
    public ImmutableArray<PexVariable> Variables { get; set; } = ImmutableArray<PexVariable>.Empty;
    public ImmutableArray<PexGuard> Guards { get; set; } = ImmutableArray<PexGuard>.Empty;
    public ImmutableArray<PexProperty> Properties { get; set; } = ImmutableArray<PexProperty>.Empty;
    public ImmutableArray<PexState> States { get; set; } = ImmutableArray<PexState>.Empty;
}

public sealed class PexStruct
{
    public string Name { get; set; } = "";
    public ImmutableArray<PexStructMember> Members { get; set; } = ImmutableArray<PexStructMember>.Empty;
}

public sealed class PexStructMember
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public uint UserFlags { get; set; }
    public PexValue DefaultValue { get; set; } = PexValue.None;
    public bool IsConst { get; set; }
    public string DocumentationString { get; set; } = "";
}

public sealed class PexGuard
{
    public string Name { get; set; } = "";
}

public sealed class PexVariable
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public uint UserFlags { get; set; }
    public PexValue DefaultValue { get; set; } = PexValue.None;
    public bool IsConst { get; set; }
}

public sealed class PexProperty
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string DocumentationString { get; set; } = "";
    public uint UserFlags { get; set; }
    public string AutoVar { get; set; } = "";
    public PexFunction? ReadFunction { get; set; }
    public PexFunction? WriteFunction { get; set; }
    public bool IsAuto { get; set; }
    public bool IsReadable { get; set; }
    public bool IsWritable { get; set; }
}
