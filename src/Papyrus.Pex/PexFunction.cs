using System.Collections.Immutable;

namespace Papyrus.Pex;

public sealed class PexState
{
    public string Name { get; set; } = "";
    public ImmutableArray<PexFunction> Functions { get; set; } = ImmutableArray<PexFunction>.Empty;
}

public sealed class PexFunction
{
    public string Name { get; set; } = "";
    public string ReturnTypeName { get; set; } = "";
    public string DocumentationString { get; set; } = "";
    public uint UserFlags { get; set; }
    public bool IsNative { get; set; }
    public bool IsGlobal { get; set; }
    public ImmutableArray<PexFunctionParameter> Parameters { get; set; } = ImmutableArray<PexFunctionParameter>.Empty;
    public ImmutableArray<PexLocalVariable> Locals { get; set; } = ImmutableArray<PexLocalVariable>.Empty;
    public ImmutableArray<PexInstruction> Instructions { get; set; } = ImmutableArray<PexInstruction>.Empty;
}

public sealed class PexFunctionParameter
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
}

public sealed class PexLocalVariable
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
}
