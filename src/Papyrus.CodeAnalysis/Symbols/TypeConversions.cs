using System;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Symbols;

public static class TypeConversions
{
    public static bool TryImplicit(ITypeSymbol source, ITypeSymbol destination, bool sourceIsNoneLiteral = false)
    {
        return ClassifyImplicit(source, destination, sourceIsNoneLiteral).Kind
            is ConversionKind.Identity or ConversionKind.Implicit;
    }

    public static Conversion ClassifyImplicit(
        ITypeSymbol source,
        ITypeSymbol destination,
        bool sourceIsNoneLiteral = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var kind = TypeAssignability.Classify(source, destination);
        if (kind is ConversionKind.Identity or ConversionKind.Implicit)
        {
            return new Conversion(kind, destination);
        }

        // None literal may be assigned to Script/Struct/Array/Var; a None-typed value may not.
        if (sourceIsNoneLiteral && IsNone(source) && IsNoneLiteralTarget(destination))
        {
            return new Conversion(ConversionKind.Implicit, destination);
        }

        return CannotConvert(source, destination);
    }

    public static Conversion ClassifyAs(ITypeSymbol source, ITypeSymbol destination, NameTable names)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(names);

        var kind = TypeAssignability.Classify(source, destination);
        if (kind is ConversionKind.Identity or ConversionKind.Implicit or ConversionKind.Explicit)
        {
            return new Conversion(kind, destination);
        }

        if (source is IScriptSymbol && destination is IScriptSymbol)
        {
            var reverse = TypeAssignability.Classify(destination, source);
            if (reverse is ConversionKind.Identity or ConversionKind.Implicit)
            {
                return new Conversion(ConversionKind.Explicit, destination);
            }

            // Unrelated script As yields None at runtime; not a conversion error.
            return new Conversion(ConversionKind.None, IntrinsicTypeSymbol.Create(names, "None"));
        }

        return CannotConvert(source, destination);
    }

    public static Conversion ClassifyIs(
        ITypeSymbol source,
        ITypeSymbol testType,
        PapyrusDialect dialect,
        NameTable names)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(testType);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(names);

        var boolean = IntrinsicTypeSymbol.Create(names, "Bool");
        if (!dialect.SupportsIsOperator)
        {
            return new Conversion(
                ConversionKind.None,
                boolean,
                new Diagnostic(new DiagnosticInfo(DiagnosticCatalog.InvalidIsOperator), default));
        }

        return new Conversion(ConversionKind.Identity, boolean);
    }

    public static Conversion ClassifyElementAssignment(
        ITypeSymbol value,
        ITypeSymbol arrayType,
        bool valueIsNoneLiteral = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(arrayType);

        var element = arrayType.GetArrayElementType();
        if (element is null)
        {
            return CannotConvert(value, arrayType);
        }

        return ClassifyImplicit(value, element, valueIsNoneLiteral);
    }

    public static ITypeSymbol CreateArrayType(
        ITypeSymbol elementType,
        NameTable names,
        PapyrusDialect? dialect = null)
    {
        return new ArrayTypeSymbol(elementType, names, dialect);
    }

    private static Conversion CannotConvert(ITypeSymbol source, ITypeSymbol destination)
    {
        return new Conversion(
            ConversionKind.None,
            destination,
            new Diagnostic(
                new DiagnosticInfo(DiagnosticCatalog.CannotConvert, source.TypeName, destination.TypeName),
                default(TextSpan)));
    }

    private static bool IsNone(ITypeSymbol type)
        => type.TypeKind == TypeKind.Intrinsic
            && type.Name.Text.Equals("None", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoneLiteralTarget(ITypeSymbol destination)
        => destination.TypeKind is TypeKind.Script or TypeKind.Struct or TypeKind.Array or TypeKind.Var;
}
