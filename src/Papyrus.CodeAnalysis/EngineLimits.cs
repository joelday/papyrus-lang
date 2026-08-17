namespace Papyrus.CodeAnalysis;

public sealed record EngineLimits
{
    public required int MaxArrayElements { get; init; }          // 128
    public required int MaxFunctionsInEmptyState { get; init; }  // 2047
    public required int MaxFunctionsPerNamedState { get; init; } // 511
    public required int MaxProperties { get; init; }             // 1023
    public required int MaxVariables { get; init; }              // 1023
    public required int MaxInitialValues { get; init; }          // 1023
    public required int MaxNamedStates { get; init; }            // 127
    public required int MaxParameters { get; init; }             // 511
    public required int MaxUserFlags { get; init; }              // 31
    public required int MaxGuards { get; init; }                 // 511 (Starfield; 0 elsewhere)
}
