using System;

namespace Papyrus.CodeAnalysis.Symbols;

public readonly struct Conversion
{
    public Conversion(ConversionKind kind, ITypeSymbol resultType, Diagnostic? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        Kind = kind;
        ResultType = resultType;
        Diagnostic = diagnostic;
    }

    public ConversionKind Kind { get; }

    public ITypeSymbol ResultType { get; }

    public Diagnostic? Diagnostic { get; }

    public bool IsError => Diagnostic is not null;
}
