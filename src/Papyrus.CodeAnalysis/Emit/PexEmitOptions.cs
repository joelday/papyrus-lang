namespace Papyrus.CodeAnalysis.Emit;

internal sealed class PexEmitOptions
{
    public static PexEmitOptions Default { get; } = new();

    public bool EmitDebugInfo { get; init; } = true;

    public bool Release { get; init; }

    internal bool IncludeDebugInfo => EmitDebugInfo && !Release;
}
