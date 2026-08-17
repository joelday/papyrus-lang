using System;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis;

public static class DiagnosticCatalog
{
    // Reserved: PAP0001–PAP0999 syntax, PAP1000–PAP1999 binding/types,
    // PAP2000–PAP2999 emit/engine limits, PAP8000–PAP8999 workspace, PAP9999 internal.
    public const string UnresolvedScriptId = "PAP1000";
    public const string UnresolvedBaseTypeId = "PAP1001";
    public const string CyclicBaseTypeId = "PAP1002";
    public const string UnresolvedTypeId = "PAP1003";
    public const string InvalidIsOperatorId = "PAP1004";
    public const string CannotConvertId = "PAP1005";
    public const string UnsupportedStructId = "PAP1006";
    public const string UnsupportedGroupId = "PAP1007";
    public const string UnsupportedCustomEventId = "PAP1008";
    public const string UnsupportedGuardId = "PAP1009";
    public const string UnsupportedNamespaceId = "PAP1010";
    public const string ReservedMemberNameId = "PAP1011";
    public const string InternalErrorId = "PAP9999";

    public const string BindingCategory = "Binding";
    public const string TypeCategory = "Type";
    public const string InternalCategory = "Internal";

    public static DiagnosticDescriptor UnresolvedScript { get; } = new(
        UnresolvedScriptId,
        "Unresolved script",
        "Script '{0}' could not be resolved.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnresolvedBaseType { get; } = new(
        UnresolvedBaseTypeId,
        "Unresolved base type",
        "Base type '{0}' could not be resolved.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CyclicBaseType { get; } = new(
        CyclicBaseTypeId,
        "Cyclic inheritance",
        "Script '{0}' has a cyclic inheritance chain.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnresolvedType { get; } = new(
        UnresolvedTypeId,
        "Unresolved type",
        "Type '{0}' could not be resolved.",
        TypeCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidIsOperator { get; } = new(
        InvalidIsOperatorId,
        "Is operator is not supported",
        "The 'is' operator is not supported in this dialect.",
        TypeCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor CannotConvert { get; } = new(
        CannotConvertId,
        "Cannot convert type",
        "Cannot convert '{0}' to '{1}'.",
        TypeCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedStruct { get; } = new(
        UnsupportedStructId,
        "Structs are not supported",
        "Structs are not supported in this dialect.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedGroup { get; } = new(
        UnsupportedGroupId,
        "Groups are not supported",
        "Groups are not supported in this dialect.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedCustomEvent { get; } = new(
        UnsupportedCustomEventId,
        "Custom events are not supported",
        "Custom events are not supported in this dialect.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedGuard { get; } = new(
        UnsupportedGuardId,
        "Guards are not supported",
        "Guards are not supported in this dialect.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedNamespace { get; } = new(
        UnsupportedNamespaceId,
        "Namespaces are not supported",
        "Namespaces are not supported in this dialect.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ReservedMemberName { get; } = new(
        ReservedMemberNameId,
        "Reserved member name",
        "Cannot declare a member named '{0}'.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InternalError { get; } = new(
        InternalErrorId,
        "Internal compiler error",
        "Internal compiler error: {0}",
        InternalCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
    [
        UnresolvedScript,
        UnresolvedBaseType,
        CyclicBaseType,
        UnresolvedType,
        InvalidIsOperator,
        CannotConvert,
        UnsupportedStruct,
        UnsupportedGroup,
        UnsupportedCustomEvent,
        UnsupportedGuard,
        UnsupportedNamespace,
        ReservedMemberName,
        InternalError,
    ];

    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> s_byId = Index(All);

    public static bool TryGet(string id, out DiagnosticDescriptor descriptor)
    {
        if (id is not null && s_byId.TryGetValue(id, out descriptor!))
        {
            return true;
        }

        descriptor = null!;
        return false;
    }

    private static ImmutableDictionary<string, DiagnosticDescriptor> Index(
        ImmutableArray<DiagnosticDescriptor> descriptors)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, DiagnosticDescriptor>(StringComparer.Ordinal);
        for (var i = 0; i < descriptors.Length; i++)
        {
            builder.Add(descriptors[i].Id, descriptors[i]);
        }

        return builder.ToImmutable();
    }
}
