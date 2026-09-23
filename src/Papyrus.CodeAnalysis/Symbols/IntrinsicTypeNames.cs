using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Papyrus.CodeAnalysis.Symbols;

/// <summary>
/// The built-in type names, shared by every place that resolves a type name.
/// </summary>
internal static class IntrinsicTypeNames
{
    public const string CustomEventName = "CustomEventName";
    public const string ScriptEventName = "ScriptEventName";

    /// <summary>
    /// True for <c>Int</c>, <c>Float</c>, <c>String</c>, <c>Bool</c>, <c>None</c>, and <c>Var</c>,
    /// plus -- in dialects with custom events (FO4+) -- <c>CustomEventName</c> and
    /// <c>ScriptEventName</c>. Those two only appear as native parameter types
    /// (<c>ScriptObject.RegisterForCustomEvent</c> and friends); in Skyrim they are
    /// ordinary identifiers.
    /// </summary>
    public static bool IsIntrinsic(string text, PapyrusDialect? dialect)
    {
        return text.Equals("Int", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Float", StringComparison.OrdinalIgnoreCase)
            || text.Equals("String", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Bool", StringComparison.OrdinalIgnoreCase)
            || text.Equals("None", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Var", StringComparison.OrdinalIgnoreCase)
            || (dialect is { SupportsCustomEvents: true } && IsEventName(text));
    }

    /// <summary>
    /// True when <paramref name="type"/> is <c>CustomEventName</c> or <c>ScriptEventName</c>.
    /// Both are strings at runtime and in PEX; arguments passed to them are event-name
    /// string literals (see <c>BoundTreeBuilder.BindArguments</c>).
    /// </summary>
    public static bool IsEventName(ITypeSymbol? type, out bool isCustomEvent)
    {
        isCustomEvent = false;
        if (type is not { TypeKind: TypeKind.Intrinsic })
        {
            return false;
        }

        isCustomEvent = type.Name.Text.Equals(CustomEventName, StringComparison.OrdinalIgnoreCase);
        return isCustomEvent || type.Name.Text.Equals(ScriptEventName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEventName(string text)
    {
        return text.Equals(CustomEventName, StringComparison.OrdinalIgnoreCase)
            || text.Equals(ScriptEventName, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Lookups that, like Caprica's <c>tryResolveStruct</c> / <c>tryResolveCustomEvent</c>,
/// search a script and then each of its parents.
/// </summary>
internal static class ScriptHierarchy
{
    public static IStructSymbol? FindStruct(
        IScriptSymbol script,
        PapyrusName name,
        Func<IScriptSymbol, ImmutableArray<ISymbol>> getMembers)
    {
        return Find<IStructSymbol>(script, name, getMembers);
    }

    /// <summary>
    /// Splits <c>A:B:C</c> into owner script <c>A:B</c> and struct name <c>C</c>.
    /// </summary>
    public static bool TrySplitStructName(QualifiedName name, out QualifiedName owner, out PapyrusName structName)
    {
        var parts = name.Parts;
        if (parts.IsDefault || parts.Length < 2)
        {
            owner = default;
            structName = default;
            return false;
        }

        owner = new QualifiedName(parts.RemoveAt(parts.Length - 1));
        structName = parts[^1];
        return true;
    }

    /// <summary>
    /// The member a non-call use of <paramref name="name"/> refers to: a property, variable,
    /// or other non-function member if the type or a base declares one, else a function.
    /// (MS13Script has both `Function DivideSpoils()` and `Bool Property DivideSpoils`;
    /// `kmyQuest.DivideSpoils = false` means the property.)
    /// </summary>
    public static ISymbol? FindMember(ITypeSymbol? type, PapyrusName name, bool includeBase = true)
    {
        ISymbol? function = null;
        var visited = new HashSet<ITypeSymbol>(ReferenceEqualityComparer.Instance);
        for (var current = type; current is not null && current.TypeKind != TypeKind.Error && visited.Add(current);)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IFunctionSymbol)
                {
                    function ??= member;
                }
                else
                {
                    return member;
                }
            }

            current = includeBase && current is IScriptSymbol script ? script.BaseType : null;
        }

        return function;
    }

    public static ICustomEventSymbol? FindCustomEvent(IScriptSymbol script, PapyrusName name)
    {
        return Find<ICustomEventSymbol>(script, name, static s => s.GetMembers());
    }

    private static T? Find<T>(
        IScriptSymbol script,
        PapyrusName name,
        Func<IScriptSymbol, ImmutableArray<ISymbol>> getMembers)
        where T : class, ISymbol
    {
        // Extends cycles are reported (PAP1002) elsewhere; just don't loop on them.
        var visited = new HashSet<IScriptSymbol>(ReferenceEqualityComparer.Instance);
        for (IScriptSymbol? current = script;
            current is not null && current.TypeKind != TypeKind.Error && visited.Add(current);
            current = current.BaseType)
        {
            var members = getMembers(current);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is T match && match.Name.Equals(name))
                {
                    return match;
                }
            }
        }

        return null;
    }
}
