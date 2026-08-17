using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis;

public sealed class PapyrusDialect
{
    private PapyrusDialect(FrozenDictionary<string, SyntaxKind> keywords, bool fo4Plus, bool starfield)
    {
        Keywords = keywords;
        SupportsFo4Plus = fo4Plus;
        SupportsStarfield = starfield;
    }

    public FrozenDictionary<string, SyntaxKind> Keywords { get; }

    public bool SupportsFo4Plus { get; }

    public bool SupportsStarfield { get; }

    public static PapyrusDialect SkyrimSe { get; } = Create(fo4Plus: false, starfield: false);

    public static PapyrusDialect Fallout4 { get; } = Create(fo4Plus: true, starfield: false);

    public static PapyrusDialect Starfield { get; } = Create(fo4Plus: true, starfield: true);

    public static PapyrusDialect Create(bool fo4Plus, bool starfield)
    {
        return new PapyrusDialect(BuildKeywords(fo4Plus, starfield), fo4Plus, starfield);
    }

    internal static PapyrusDialect FromOptions(PapyrusParseOptions options)
    {
        var fo4Plus = options.Fo4Plus || options.Starfield;
        var starfield = options.Starfield;
        if (!fo4Plus && !starfield)
        {
            return SkyrimSe;
        }

        if (fo4Plus && !starfield)
        {
            return Fallout4;
        }

        return Starfield;
    }

    private static FrozenDictionary<string, SyntaxKind> BuildKeywords(bool fo4Plus, bool starfield)
    {
        var map = new Dictionary<string, SyntaxKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["As"] = SyntaxKind.AsKeyword,
            ["Auto"] = SyntaxKind.AutoKeyword,
            ["AutoReadOnly"] = SyntaxKind.AutoReadOnlyKeyword,
            ["Bool"] = SyntaxKind.BoolKeyword,
            ["Conditional"] = SyntaxKind.ConditionalKeyword,
            ["Else"] = SyntaxKind.ElseKeyword,
            ["ElseIf"] = SyntaxKind.ElseIfKeyword,
            ["EndEvent"] = SyntaxKind.EndEventKeyword,
            ["EndFunction"] = SyntaxKind.EndFunctionKeyword,
            ["EndIf"] = SyntaxKind.EndIfKeyword,
            ["EndProperty"] = SyntaxKind.EndPropertyKeyword,
            ["EndState"] = SyntaxKind.EndStateKeyword,
            ["EndWhile"] = SyntaxKind.EndWhileKeyword,
            ["Event"] = SyntaxKind.EventKeyword,
            ["Extends"] = SyntaxKind.ExtendsKeyword,
            ["False"] = SyntaxKind.FalseKeyword,
            ["Float"] = SyntaxKind.FloatKeyword,
            ["Function"] = SyntaxKind.FunctionKeyword,
            ["Global"] = SyntaxKind.GlobalKeyword,
            ["Hidden"] = SyntaxKind.HiddenKeyword,
            ["If"] = SyntaxKind.IfKeyword,
            ["Import"] = SyntaxKind.ImportKeyword,
            ["Int"] = SyntaxKind.IntKeyword,
            ["Is"] = SyntaxKind.IsKeyword,
            ["Length"] = SyntaxKind.LengthKeyword,
            ["Native"] = SyntaxKind.NativeKeyword,
            ["New"] = SyntaxKind.NewKeyword,
            ["None"] = SyntaxKind.NoneKeyword,
            ["Property"] = SyntaxKind.PropertyKeyword,
            ["Return"] = SyntaxKind.ReturnKeyword,
            ["ScriptName"] = SyntaxKind.ScriptNameKeyword,
            ["State"] = SyntaxKind.StateKeyword,
            ["String"] = SyntaxKind.StringKeyword,
            ["True"] = SyntaxKind.TrueKeyword,
            ["While"] = SyntaxKind.WhileKeyword,
        };

        if (fo4Plus)
        {
            map["BetaOnly"] = SyntaxKind.BetaOnlyKeyword;
            map["Const"] = SyntaxKind.ConstKeyword;
            map["CustomEvent"] = SyntaxKind.CustomEventKeyword;
            map["CustomEventName"] = SyntaxKind.CustomEventNameKeyword;
            map["DebugOnly"] = SyntaxKind.DebugOnlyKeyword;
            map["EndGroup"] = SyntaxKind.EndGroupKeyword;
            map["EndStruct"] = SyntaxKind.EndStructKeyword;
            map["Group"] = SyntaxKind.GroupKeyword;
            map["Mandatory"] = SyntaxKind.MandatoryKeyword;
            map["ScriptEventName"] = SyntaxKind.ScriptEventNameKeyword;
            map["Struct"] = SyntaxKind.StructKeyword;
            map["Var"] = SyntaxKind.VarKeyword;
        }

        if (starfield)
        {
            map["EndGuard"] = SyntaxKind.EndGuardKeyword;
            map["Guard"] = SyntaxKind.GuardKeyword;
            map["TryGuard"] = SyntaxKind.TryGuardKeyword;
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
