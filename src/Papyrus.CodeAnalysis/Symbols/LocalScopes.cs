using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis.Symbols;

/// <summary>
/// Block scoping for function locals. A function's locals are one flat list, but a name
/// may be declared again in a sibling block (often with a different type), so a use must
/// resolve to the declaration whose block encloses it.
/// </summary>
internal static class LocalScopes
{
    /// <summary>
    /// The local named <paramref name="name"/> that a use at <paramref name="position"/>
    /// refers to: the nearest preceding in-scope declaration, else (Skyrim allows use
    /// before declaration) the first in-scope one, else the first with that name.
    /// </summary>
    public static IVariableSymbol? Find(ImmutableArray<IVariableSymbol> locals, PapyrusName name, int position)
    {
        IVariableSymbol? first = null;
        IVariableSymbol? preceding = null;
        IVariableSymbol? following = null;
        var precedingStart = -1;
        var followingStart = int.MaxValue;
        foreach (var local in locals)
        {
            if (!local.Name.Equals(name))
            {
                continue;
            }

            first ??= local;
            if (local is not VariableSymbol { LocalDeclaration: { } declaration } || !InScope(declaration, position))
            {
                continue;
            }

            var start = declaration.Span.Start;
            if (start <= position)
            {
                if (start > precedingStart)
                {
                    preceding = local;
                    precedingStart = start;
                }
            }
            else if (start < followingStart)
            {
                following = local;
                followingStart = start;
            }
        }

        return preceding ?? following ?? first;
    }

    /// <summary>The local declared by <paramref name="declare"/>.</summary>
    public static IVariableSymbol? FindDeclared(ImmutableArray<IVariableSymbol> locals, DeclareStatementSyntax declare)
    {
        foreach (var local in locals)
        {
            if (local is VariableSymbol { LocalDeclaration: { } declaration }
                && declaration.Span == declare.Span
                && ReferenceEquals(declaration.SyntaxTree, declare.SyntaxTree))
            {
                return local;
            }
        }

        return null;
    }

    private static bool InScope(DeclareStatementSyntax declaration, int position)
    {
        if (declaration.Parent is not { } block)
        {
            return true;
        }

        var end = block.Span.End;
        if (block is IfStatementSyntax ifStatement)
        {
            // The If's own statements end where its first ElseIf/Else begins.
            if (ifStatement.ElseIfClauses.Count > 0)
            {
                end = ifStatement.ElseIfClauses.Get(0).Span.Start;
            }
            else if (ifStatement.ElseClause is { } elseClause)
            {
                end = elseClause.Span.Start;
            }
        }

        return position >= block.Span.Start && position < end;
    }
}
