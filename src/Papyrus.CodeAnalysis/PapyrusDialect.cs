using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis;

public sealed record PapyrusDialect
{
    public required GameId Game { get; init; }
    public required LanguageVersion Version { get; init; }

    public required FrozenDictionary<string, SyntaxKind> Keywords { get; init; }
    public required bool SupportsStructs { get; init; }
    public required bool SupportsGroups { get; init; }
    public required bool SupportsCustomEvents { get; init; }
    public required bool SupportsNamespaces { get; init; }
    public required bool SupportsGuards { get; init; }
    public required bool SupportsIsOperator { get; init; }

    public required bool AllowUnknownEventsOnNonNativeClass { get; init; }
    public required bool AllowVariableShadowingParentProperty { get; init; }
    public required bool AllowLocalUseBeforeDeclaration { get; init; }
    public required bool AllowAssigningVoidMethodCallResult { get; init; }

    public required PexVersion PexVersion { get; init; }
    public required PexOpCode MaxOpCode { get; init; }
    public required EngineLimits Limits { get; init; }

    public static PapyrusDialect SkyrimSe { get; }
    public static PapyrusDialect Fallout4 { get; }
    public static PapyrusDialect Starfield { get; }

    // Compile-capable, no corpus gate, no LE compliance number (K26).
    public static PapyrusDialect SkyrimLe { get; }

    // Read-only. PexReader identifies the file; papyrusc will not compile for it.
    public static PapyrusDialect Fallout76 { get; }

    static PapyrusDialect()
    {
        var limits = new EngineLimits
        {
            MaxArrayElements = 128,
            MaxFunctionsInEmptyState = 2047,
            MaxFunctionsPerNamedState = 511,
            MaxProperties = 1023,
            MaxVariables = 1023,
            MaxInitialValues = 1023,
            MaxNamedStates = 127,
            MaxParameters = 511,
            MaxUserFlags = 31,
            MaxGuards = 0,
        };
        var starfieldLimits = limits with { MaxGuards = 511 };

        var skyrimKeywords = CreateKeywords(SkyrimKeywordKinds);
        var fallout4Keywords = CreateKeywords(SkyrimKeywordKinds, Fallout4KeywordKinds);
        var starfieldKeywords = CreateKeywords(SkyrimKeywordKinds, Fallout4KeywordKinds, StarfieldKeywordKinds);

        SkyrimLe = new PapyrusDialect
        {
            Game = GameId.Skyrim,
            Version = LanguageVersion.Official,
            Keywords = skyrimKeywords,
            SupportsStructs = false,
            SupportsGroups = false,
            SupportsCustomEvents = false,
            SupportsNamespaces = false,
            SupportsGuards = false,
            SupportsIsOperator = false,
            AllowUnknownEventsOnNonNativeClass = true,
            AllowVariableShadowingParentProperty = true,
            AllowLocalUseBeforeDeclaration = true,
            AllowAssigningVoidMethodCallResult = true,
            PexVersion = PexVersion.V31,
            MaxOpCode = PexOpCode.SkyrimMax,
            Limits = limits,
        };

        SkyrimSe = new PapyrusDialect
        {
            Game = GameId.SkyrimSpecialEdition,
            Version = LanguageVersion.Official,
            Keywords = skyrimKeywords,
            SupportsStructs = false,
            SupportsGroups = false,
            SupportsCustomEvents = false,
            SupportsNamespaces = false,
            SupportsGuards = false,
            SupportsIsOperator = false,
            AllowUnknownEventsOnNonNativeClass = true,
            AllowVariableShadowingParentProperty = true,
            AllowLocalUseBeforeDeclaration = true,
            AllowAssigningVoidMethodCallResult = true,
            PexVersion = PexVersion.V32,
            MaxOpCode = PexOpCode.SkyrimMax,
            Limits = limits,
        };

        Fallout4 = new PapyrusDialect
        {
            Game = GameId.Fallout4,
            Version = LanguageVersion.Official,
            Keywords = fallout4Keywords,
            SupportsStructs = true,
            SupportsGroups = true,
            SupportsCustomEvents = true,
            SupportsNamespaces = true,
            SupportsGuards = false,
            SupportsIsOperator = true,
            AllowUnknownEventsOnNonNativeClass = false,
            AllowVariableShadowingParentProperty = false,
            AllowLocalUseBeforeDeclaration = false,
            AllowAssigningVoidMethodCallResult = false,
            PexVersion = PexVersion.V39,
            MaxOpCode = PexOpCode.Fallout4Max,
            Limits = limits,
        };

        Starfield = new PapyrusDialect
        {
            Game = GameId.Starfield,
            Version = LanguageVersion.Official,
            Keywords = starfieldKeywords,
            SupportsStructs = true,
            SupportsGroups = true,
            SupportsCustomEvents = true,
            SupportsNamespaces = true,
            SupportsGuards = true,
            SupportsIsOperator = true,
            AllowUnknownEventsOnNonNativeClass = false,
            AllowVariableShadowingParentProperty = false,
            AllowLocalUseBeforeDeclaration = false,
            AllowAssigningVoidMethodCallResult = false,
            PexVersion = PexVersion.V312,
            MaxOpCode = PexOpCode.StarfieldMax,
            Limits = starfieldLimits,
        };

        Fallout76 = new PapyrusDialect
        {
            Game = GameId.Fallout76,
            Version = LanguageVersion.Official,
            Keywords = fallout4Keywords,
            SupportsStructs = true,
            SupportsGroups = true,
            SupportsCustomEvents = true,
            SupportsNamespaces = true,
            SupportsGuards = false,
            SupportsIsOperator = true,
            AllowUnknownEventsOnNonNativeClass = false,
            AllowVariableShadowingParentProperty = false,
            AllowLocalUseBeforeDeclaration = false,
            AllowAssigningVoidMethodCallResult = false,
            PexVersion = PexVersion.V315,
            MaxOpCode = PexOpCode.Fallout76Max,
            Limits = limits,
        };
    }

    // Self and Parent are identifiers; the binder assigns their meaning.
    // Is is omitted on Skyrim: SupportsIsOperator is unsettled and ships false.
    private static readonly SyntaxKind[] SkyrimKeywordKinds =
    {
        SyntaxKind.AsKeyword,
        SyntaxKind.AutoKeyword,
        SyntaxKind.AutoReadOnlyKeyword,
        SyntaxKind.BoolKeyword,
        SyntaxKind.ConditionalKeyword,
        SyntaxKind.ElseKeyword,
        SyntaxKind.ElseIfKeyword,
        SyntaxKind.EndEventKeyword,
        SyntaxKind.EndFunctionKeyword,
        SyntaxKind.EndIfKeyword,
        SyntaxKind.EndPropertyKeyword,
        SyntaxKind.EndStateKeyword,
        SyntaxKind.EndWhileKeyword,
        SyntaxKind.EventKeyword,
        SyntaxKind.ExtendsKeyword,
        SyntaxKind.FalseKeyword,
        SyntaxKind.FloatKeyword,
        SyntaxKind.FunctionKeyword,
        SyntaxKind.GlobalKeyword,
        SyntaxKind.HiddenKeyword,
        SyntaxKind.IfKeyword,
        SyntaxKind.ImportKeyword,
        SyntaxKind.IntKeyword,
        SyntaxKind.LengthKeyword,
        SyntaxKind.NativeKeyword,
        SyntaxKind.NewKeyword,
        SyntaxKind.NoneKeyword,
        SyntaxKind.PropertyKeyword,
        SyntaxKind.ReturnKeyword,
        SyntaxKind.ScriptNameKeyword,
        SyntaxKind.StateKeyword,
        SyntaxKind.StringKeyword,
        SyntaxKind.TrueKeyword,
        SyntaxKind.WhileKeyword,
    };

    private static readonly SyntaxKind[] Fallout4KeywordKinds =
    {
        SyntaxKind.BetaOnlyKeyword,
        SyntaxKind.ConstKeyword,
        SyntaxKind.CustomEventKeyword,
        SyntaxKind.CustomEventNameKeyword,
        SyntaxKind.DebugOnlyKeyword,
        SyntaxKind.EndGroupKeyword,
        SyntaxKind.EndStructKeyword,
        SyntaxKind.GroupKeyword,
        SyntaxKind.IsKeyword,
        SyntaxKind.ScriptEventNameKeyword,
        SyntaxKind.StructKeyword,
        SyntaxKind.VarKeyword,
    };

    private static readonly SyntaxKind[] StarfieldKeywordKinds =
    {
        SyntaxKind.EndGuardKeyword,
        SyntaxKind.EndLockKeyword,
        SyntaxKind.GuardKeyword,
        SyntaxKind.LockKeyword,
        SyntaxKind.TryGuardKeyword,
        SyntaxKind.TryLockKeyword,
        SyntaxKind.UnlockKeyword,
    };

    private static FrozenDictionary<string, SyntaxKind> CreateKeywords(params SyntaxKind[][] groups)
    {
        var builder = new Dictionary<string, SyntaxKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var kind in group)
            {
                var text = SyntaxFacts.GetText(kind);
                if (text is null)
                {
                    throw new InvalidOperationException("SyntaxKind." + kind + " has no canonical text.");
                }

                builder.Add(text, kind);
            }
        }

        return builder.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
