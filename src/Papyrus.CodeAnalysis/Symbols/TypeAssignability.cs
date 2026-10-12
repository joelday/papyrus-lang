using System;
using System.Collections.Generic;

namespace Papyrus.CodeAnalysis.Symbols;

public static class TypeAssignability
{
    public static bool IsAssignable(ITypeSymbol source, ITypeSymbol destination)
    {
        var kind = Classify(source, destination);
        return kind is ConversionKind.Identity or ConversionKind.Implicit;
    }

    public static ConversionKind Classify(ITypeSymbol source, ITypeSymbol destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        // Error types are assignable both ways so one bad type does not cascade.
        if (source.TypeKind == TypeKind.Error || destination.TypeKind == TypeKind.Error)
        {
            return ConversionKind.Identity;
        }

        if (AreSameType(source, destination))
        {
            return ConversionKind.Identity;
        }

        if (IsNone(source))
        {
            // None → T is literal-only / allowlist; not implicit here.
            return ConversionKind.None;
        }

        if (IsNone(destination))
        {
            return source.TypeKind is TypeKind.Script or TypeKind.Struct or TypeKind.Array or TypeKind.Var
                ? ConversionKind.Implicit
                : ConversionKind.None;
        }

        if (destination.TypeKind == TypeKind.Var)
        {
            return ConversionKind.Implicit;
        }

        if (source.TypeKind == TypeKind.Var)
        {
            return ConversionKind.Explicit;
        }

        if (source.TypeKind == TypeKind.Array && destination.TypeKind == TypeKind.Array)
        {
            // Same element only — arrays are not covariant. Identity includes error recovery.
            var sourceElement = source.GetArrayElementType();
            var destinationElement = destination.GetArrayElementType();
            if (sourceElement is not null
                && destinationElement is not null
                && Classify(sourceElement, destinationElement) == ConversionKind.Identity)
            {
                return ConversionKind.Identity;
            }

            return ConversionKind.None;
        }

        if (source is IScriptSymbol sourceScript)
        {
            if (destination is IScriptSymbol destinationScript
                && IsDerivedFrom(sourceScript, destinationScript))
            {
                return ConversionKind.Implicit;
            }

            if (IsNamed(destination, "String"))
            {
                return ConversionKind.Explicit;
            }

            return ConversionKind.None;
        }

        return ClassifyPrimitives(source, destination);
    }

    private static ConversionKind ClassifyPrimitives(ITypeSymbol source, ITypeSymbol destination)
    {
        if (source.TypeKind != TypeKind.Intrinsic || destination.TypeKind != TypeKind.Intrinsic)
        {
            return ConversionKind.None;
        }

        if (IsNamed(source, "Int"))
        {
            if (IsNamed(destination, "Bool") || IsNamed(destination, "Float"))
            {
                return ConversionKind.Implicit;
            }

            if (IsNamed(destination, "String"))
            {
                return ConversionKind.Explicit;
            }

            return ConversionKind.None;
        }

        if (IsNamed(source, "Float"))
        {
            if (IsNamed(destination, "Bool"))
            {
                return ConversionKind.Implicit;
            }

            if (IsNamed(destination, "Int") || IsNamed(destination, "String"))
            {
                return ConversionKind.Explicit;
            }

            return ConversionKind.None;
        }

        if (IsNamed(source, "Bool") && IsNamed(destination, "String"))
        {
            return ConversionKind.Explicit;
        }

        if (IsNamed(source, "String") && IsNamed(destination, "Bool"))
        {
            return ConversionKind.Implicit;
        }

        return ConversionKind.None;
    }

    private static bool AreSameType(ITypeSymbol source, ITypeSymbol destination)
    {
        if (ReferenceEquals(source, destination))
        {
            return true;
        }

        if (source.TypeKind != destination.TypeKind)
        {
            return false;
        }

        if (source.TypeKind == TypeKind.Array)
        {
            var sourceElement = source.GetArrayElementType();
            var destinationElement = destination.GetArrayElementType();
            return sourceElement is not null
                && destinationElement is not null
                && AreSameType(sourceElement, destinationElement);
        }

        if (source is IScriptSymbol sourceScript && destination is IScriptSymbol destinationScript)
        {
            return sourceScript.QualifiedName.Equals(destinationScript.QualifiedName);
        }

        return source.TypeName.Equals(destination.TypeName);
    }

    private static bool IsDerivedFrom(IScriptSymbol source, IScriptSymbol destination)
    {
        var seen = new HashSet<IScriptSymbol>(ReferenceEqualityComparer.Instance) { source };
        for (var current = source.BaseType; current is not null; current = current.BaseType)
        {
            if (!seen.Add(current) || current.TypeKind == TypeKind.Error)
            {
                return false;
            }

            if (current.QualifiedName.Equals(destination.QualifiedName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNone(ITypeSymbol type)
        => type.TypeKind == TypeKind.Intrinsic && IsNamed(type, "None");

    private static bool IsNamed(ITypeSymbol type, string name)
        => type.Name.Text.Equals(name, StringComparison.OrdinalIgnoreCase);
}
