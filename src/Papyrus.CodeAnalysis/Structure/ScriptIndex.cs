using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Papyrus.CodeAnalysis.Syntax;
using GreenToken = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Structure;

public enum IndexedDeclarationKind
{
    Script,
    Function,
    Event,
    Property,
    Variable,
    Struct,
    StructMember,
    State,
    Group,
    CustomEvent,
    Guard,
}

/// <summary>A declaration found by <see cref="ScriptIndex"/>, located by its name.</summary>
public readonly record struct IndexedDeclaration(
    string Name,
    IndexedDeclarationKind Kind,
    TextSpan NameSpan,
    string? Container);

/// <summary>
/// What a script declares and which names it mentions, from syntax alone (no binding), so
/// workspace-wide features can skip the scripts that cannot matter: workspace symbol
/// search reads <see cref="Declarations"/>, and reference search binds only the scripts
/// whose <see cref="MentionsName"/> is true. Cached per tree, so it is rebuilt only for
/// scripts that changed.
/// </summary>
public sealed class ScriptIndex
{
    private static readonly ConditionalWeakTable<SyntaxTree, ScriptIndex> s_cache = new();

    private readonly HashSet<string> _identifiers;

    private ScriptIndex(ImmutableArray<IndexedDeclaration> declarations, HashSet<string> identifiers)
    {
        Declarations = declarations;
        _identifiers = identifiers;
    }

    public ImmutableArray<IndexedDeclaration> Declarations { get; }

    public static ScriptIndex Get(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return s_cache.GetValue(tree, static t => Build(t));
    }

    /// <summary>Whether any identifier in the script is <paramref name="name"/> (case-insensitive).</summary>
    public bool MentionsName(string name) => _identifiers.Contains(name);

    private static ScriptIndex Build(SyntaxTree tree)
    {
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectIdentifiers(tree.GetRoot().Green, identifiers);

        var declarations = ImmutableArray.CreateBuilder<IndexedDeclaration>();
        if (tree.TryGetRoot(out var root) && !root.Name.IsMissing)
        {
            var scriptName = root.Name.ToFullString().Trim();
            declarations.Add(new IndexedDeclaration(scriptName, IndexedDeclarationKind.Script, root.Name.Span, null));
            AddMembers(root.Members, scriptName, declarations);
        }

        return new ScriptIndex(declarations.ToImmutable(), identifiers);
    }

    private static void AddMembers<T>(
        SyntaxList<T> members,
        string container,
        ImmutableArray<IndexedDeclaration>.Builder declarations)
        where T : SyntaxNode
    {
        for (var i = 0; i < members.Count; i++)
        {
            switch (members.Get(i))
            {
                case FunctionDeclarationSyntax function:
                    Add(function.Name, IndexedDeclarationKind.Function, container, declarations);
                    break;
                case EventDeclarationSyntax @event:
                    Add(@event.Name, IndexedDeclarationKind.Event, container, declarations);
                    break;
                case PropertyDeclarationSyntax property:
                    Add(property.Name, IndexedDeclarationKind.Property, container, declarations);
                    break;
                case VariableDeclarationSyntax variable:
                    Add(
                        variable.Name,
                        container.Contains('.', StringComparison.Ordinal)
                            ? IndexedDeclarationKind.StructMember
                            : IndexedDeclarationKind.Variable,
                        container,
                        declarations);
                    break;
                case StateDeclarationSyntax state:
                    Add(state.Name, IndexedDeclarationKind.State, container, declarations);
                    AddMembers(state.Members, container + "." + state.Name.Identifier.Text, declarations);
                    break;
                case StructDeclarationSyntax struc:
                    Add(struc.Name, IndexedDeclarationKind.Struct, container, declarations);
                    AddMembers(struc.Members, container + "." + struc.Name.Identifier.Text, declarations);
                    break;
                case GroupDeclarationSyntax group:
                    Add(group.Name, IndexedDeclarationKind.Group, container, declarations);
                    AddMembers(group.Members, container, declarations);
                    break;
                case CustomEventDeclarationSyntax customEvent:
                    Add(customEvent.Name, IndexedDeclarationKind.CustomEvent, container, declarations);
                    break;
                case GuardDeclarationSyntax guard:
                    Add(guard.Name, IndexedDeclarationKind.Guard, container, declarations);
                    break;
            }
        }
    }

    private static void Add(
        IdentifierNameSyntax name,
        IndexedDeclarationKind kind,
        string container,
        ImmutableArray<IndexedDeclaration>.Builder declarations)
    {
        if (!name.IsMissing && name.Identifier.Text.Length > 0)
        {
            declarations.Add(new IndexedDeclaration(name.Identifier.Text, kind, name.Span, container));
        }
    }

    private static void CollectIdentifiers(GreenNode node, HashSet<string> identifiers)
    {
        if (node is GreenToken token)
        {
            if (token.Kind == SyntaxKind.IdentifierToken && token.Text.Length > 0)
            {
                identifiers.Add(token.Text);
            }

            return;
        }

        for (var i = 0; i < node.SlotCount; i++)
        {
            if (node.GetSlot(i) is { } child)
            {
                CollectIdentifiers(child, identifiers);
            }
        }
    }
}
