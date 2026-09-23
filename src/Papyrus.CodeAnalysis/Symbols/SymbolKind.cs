namespace Papyrus.CodeAnalysis.Symbols;

public enum SymbolKind
{
    None = 0,
    Script,
    Function,
    Event,
    Property,
    Variable,
    Parameter,
    Struct,
    State,
    CustomEvent,
    Guard,
    Array,
    Intrinsic,
}
