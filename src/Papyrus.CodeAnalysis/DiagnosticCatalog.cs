using System;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis;

public static class DiagnosticCatalog
{
    // Reserved: PAP0001–PAP0999 syntax, PAP1000–PAP1999 binding/types,
    // PAP2000–PAP2999 emit/engine limits, PAP8000–PAP8999 workspace, PAP9999 internal.
    public const string ExpectedTokenId = "PAP0001";
    public const string UnexpectedTokenId = "PAP0002";
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
    public const string UnresolvedFunctionId = "PAP1012";
    public const string UndefinedNameId = "PAP1013";
    public const string MemberNotFoundId = "PAP1014";
    public const string UserFlagBitIndexOutOfRangeId = "PAP2000";
    public const string TooManyNamedUserFlagsId = "PAP2001";
    public const string ArraySizeExceedsLimitId = "PAP2002";
    public const string TooManyFunctionsInEmptyStateId = "PAP2003";
    public const string TooManyFunctionsInNamedStateId = "PAP2004";
    public const string TooManyPropertiesId = "PAP2005";
    public const string TooManyVariablesId = "PAP2006";
    public const string TooManyInitialValuesId = "PAP2007";
    public const string TooManyNamedStatesId = "PAP2008";
    public const string TooManyParametersId = "PAP2009";
    public const string TooManyUserFlagsId = "PAP2010";
    public const string TooManyGuardsId = "PAP2011";
    public const string InvalidFlagsFileId = "PAP8001";
    public const string InternalErrorId = "PAP9999";

    public const string SyntaxCategory = "Syntax";
    public const string BindingCategory = "Binding";
    public const string TypeCategory = "Type";
    public const string EngineLimitCategory = "EngineLimit";
    public const string InternalCategory = "Internal";
    public const string WorkspaceCategory = "Workspace";

    public static DiagnosticDescriptor ExpectedToken { get; } = new(
        ExpectedTokenId,
        "Expected token",
        "Expected {0}.",
        SyntaxCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnexpectedToken { get; } = new(
        UnexpectedTokenId,
        "Unexpected token",
        "Unexpected '{0}'.",
        SyntaxCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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

    public static DiagnosticDescriptor UserFlagBitIndexOutOfRange { get; } = new(
        UserFlagBitIndexOutOfRangeId,
        "User flag bit index out of range",
        "User flag '{0}' bit index {1} is outside the 32-bit mask (0-31).",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnresolvedFunction { get; } = new(
        UnresolvedFunctionId,
        "Unresolved function",
        "Function '{0}' could not be resolved.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UndefinedName { get; } = new(
        UndefinedNameId,
        "Undefined name",
        "The name '{0}' does not exist in the current context.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MemberNotFound { get; } = new(
        MemberNotFoundId,
        "Member not found",
        "'{0}' does not contain a member named '{1}'.",
        BindingCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidFlagsFile { get; } = new(
        InvalidFlagsFileId,
        "Invalid flags file",
        "Flags file error at line {0}: {1}",
        WorkspaceCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyNamedUserFlags { get; } = new(
        TooManyNamedUserFlagsId,
        "Too many named user flags",
        "User flag '{0}' exceeds the 32 named-flag limit.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ArraySizeExceedsLimit { get; } = new(
        ArraySizeExceedsLimitId,
        "Array size exceeds engine limit",
        "Array size {0} exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyFunctionsInEmptyState { get; } = new(
        TooManyFunctionsInEmptyStateId,
        "Too many functions in empty state",
        "Empty state has {0} functions, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyFunctionsInNamedState { get; } = new(
        TooManyFunctionsInNamedStateId,
        "Too many functions in named state",
        "State '{0}' has {1} functions, which exceeds the engine limit of {2}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyProperties { get; } = new(
        TooManyPropertiesId,
        "Too many properties",
        "Script has {0} properties, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyVariables { get; } = new(
        TooManyVariablesId,
        "Too many variables",
        "Script has {0} variables, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyInitialValues { get; } = new(
        TooManyInitialValuesId,
        "Too many initial values",
        "Script has {0} initial values, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyNamedStates { get; } = new(
        TooManyNamedStatesId,
        "Too many named states",
        "Script has {0} named states, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyParameters { get; } = new(
        TooManyParametersId,
        "Too many parameters",
        "Function '{0}' has {1} parameters, which exceeds the engine limit of {2}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyUserFlags { get; } = new(
        TooManyUserFlagsId,
        "Too many user flags",
        "User flag count {0} exceeds the engine limit of {1}.",
        EngineLimitCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TooManyGuards { get; } = new(
        TooManyGuardsId,
        "Too many guards",
        "Script has {0} guards, which exceeds the engine limit of {1}.",
        EngineLimitCategory,
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
        ExpectedToken,
        UnexpectedToken,
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
        UnresolvedFunction,
        UndefinedName,
        MemberNotFound,
        UserFlagBitIndexOutOfRange,
        TooManyNamedUserFlags,
        ArraySizeExceedsLimit,
        TooManyFunctionsInEmptyState,
        TooManyFunctionsInNamedState,
        TooManyProperties,
        TooManyVariables,
        TooManyInitialValues,
        TooManyNamedStates,
        TooManyParameters,
        TooManyUserFlags,
        TooManyGuards,
        InvalidFlagsFile,
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
