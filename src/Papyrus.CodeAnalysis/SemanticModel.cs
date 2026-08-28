using System;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

public sealed class SemanticModel
{
    private readonly SyntaxNode? _speculative;
    private readonly int _position;
    private IScriptSymbol? _script;

    internal SemanticModel(PapyrusCompilation compilation, SyntaxTree tree)
        : this(compilation, tree, speculative: null, position: 0) { }

    private SemanticModel(
        PapyrusCompilation compilation,
        SyntaxTree tree,
        SyntaxNode? speculative,
        int position
    )
    {
        Compilation = compilation;
        SyntaxTree = tree;
        _speculative = speculative;
        _position = position;
    }

    public PapyrusCompilation Compilation { get; }

    public SyntaxTree SyntaxTree { get; }

    public ISymbol? GetSymbolInfo(int position, CancellationToken cancellationToken = default)
    {
        var node = FindNode(position, cancellationToken);
        return node is null ? null : GetSymbolInfo(node, cancellationToken);
    }

    /// <summary>
    /// Finds the narrowest syntax node whose span contains <paramref name="position"/>,
    /// stopping descent at the first token slot reached (mirroring Roslyn's
    /// <c>SyntaxNode.FindNode</c>). Returns <see langword="null"/> when the position
    /// falls outside the tree.
    /// </summary>
    public SyntaxNode? FindNode(int position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = SyntaxTree.GetRoot();
        if (position < root.FullSpan.Start || position >= root.FullSpan.End)
        {
            return null;
        }

        return FindNode(root, position);
    }

    private static SyntaxNode? FindNode(SyntaxNode node, int position)
    {
        var offset = node.Position;
        for (var i = 0; i < node.Green.SlotCount; i++)
        {
            var slot = node.Green.GetSlot(i);
            if (slot is null || position < offset || position >= offset + slot.FullWidth)
            {
                offset += slot?.FullWidth ?? 0;
                continue;
            }

            if (slot.Kind == SyntaxKind.List)
            {
                var listOffset = offset;
                for (var j = 0; j < slot.SlotCount; j++)
                {
                    var item = slot.GetSlot(j);
                    if (
                        item is not null
                        && position >= listOffset
                        && position < listOffset + item.FullWidth
                    )
                    {
                        return item is Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken
                            ? node
                            : FindNode(item.CreateRed(node, listOffset), position) ?? node;
                    }

                    listOffset += item?.FullWidth ?? 0;
                }
            }
            else if (slot is not Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken)
            {
                return FindNode(slot.CreateRed(node, offset), position) ?? node;
            }

            return node;
        }

        return node;
    }

    public ISymbol? GetDeclaredSymbol(
        SyntaxNode node,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        var script = GetScript();
        if (script is null)
        {
            return null;
        }

        switch (node)
        {
            case ScriptDeclarationSyntax:
                return script;
            case FunctionDeclarationSyntax:
            case EventDeclarationSyntax:
            case PropertyDeclarationSyntax:
            case VariableDeclarationSyntax:
                return FindByDeclaration(script, node);
            case ParameterSyntax parameter:
                return FindParameter(parameter);
            case StateDeclarationSyntax state:
                return FindNamed(script, state.Name, SymbolKind.State);
            case StructDeclarationSyntax struc:
                return FindNamed(script, struc.Name, SymbolKind.Struct);
            case GuardDeclarationSyntax guard:
                return FindNamed(script, guard.Name, SymbolKind.Guard);
            case CustomEventDeclarationSyntax customEvent:
                return FindNamed(script, customEvent.Name, SymbolKind.CustomEvent);
            case DeclareStatementSyntax declare:
                return FindLocal(declare);
            case IdentifierNameSyntax when node.Parent is { } parent:
                return GetDeclaredSymbol(parent, cancellationToken);
            default:
                return null;
        }
    }

    public ISymbol? GetSymbolInfo(SyntaxNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        var declared = GetDeclaredSymbol(node, cancellationToken);
        if (declared is not null)
        {
            return declared;
        }

        switch (node)
        {
            case NameExpressionSyntax name:
                return GetSymbolInfo(name.Name, cancellationToken);
            case InvocationExpressionSyntax invocation:
                return GetSymbolInfo(invocation.Expression, cancellationToken);
            case MemberAccessExpressionSyntax member:
                return BindMemberAccess(member, cancellationToken);
            case IdentifierNameSyntax identifier
                when node.Parent is MemberAccessExpressionSyntax member:
                return BindMemberAccess(member, cancellationToken);
            case IdentifierNameSyntax identifier when IsExtendsName(identifier):
                return GetScript()?.BaseType;
            case IdentifierNameSyntax identifier:
                return LookupName(identifier, ScopePosition(node));
            case NameSyntax when IsExtendsName(node):
                return GetScript()?.BaseType;
            default:
                return IsExtendsName(node) ? GetScript()?.BaseType : null;
        }
    }

    public ITypeSymbol? GetTypeInfo(SyntaxNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        if (node is ExpressionSyntax expression)
        {
            var type = new ExpressionTypeBinder(Compilation, GetScript())
                .Bind(expression)
                .ResultType;
            if (type.TypeKind != TypeKind.Error)
            {
                return type;
            }
        }

        return TypeOf(
            GetSymbolInfo(node, cancellationToken) ?? GetDeclaredSymbol(node, cancellationToken)
        );
    }

    /// <summary>
    /// Looks up the symbols reachable at <paramref name="position"/>. When
    /// <paramref name="container"/> is given, this returns its members plus any inherited
    /// members walked up its base-type chain (member-access lookup, e.g. after
    /// <c>expr.</c>). Otherwise it returns the locals and parameters visible in the
    /// enclosing function plus the containing script's own and inherited members
    /// (unqualified lookup).
    /// </summary>
    public ImmutableArray<ISymbol> LookupSymbols(int position, ITypeSymbol? container = null)
    {
        var builder = ImmutableArray.CreateBuilder<ISymbol>();
        if (container is not null)
        {
            AddMembersWithBaseTypes(container, builder);
            return builder.ToImmutable();
        }

        var function = FindContainingFunction(position);
        if (function is not null)
        {
            builder.AddRange(function.Parameters);
            builder.AddRange(function.Locals);
        }

        var script = GetScript();
        if (script is not null)
        {
            AddMembersWithBaseTypes(script, builder);
        }

        return builder.ToImmutable();
    }

    private static void AddMembersWithBaseTypes(
        ITypeSymbol container,
        ImmutableArray<ISymbol>.Builder builder
    )
    {
        for (ITypeSymbol? current = container; current is not null; )
        {
            builder.AddRange(current.GetMembers());
            current = current is IScriptSymbol script ? script.BaseType : null;
        }
    }

    /// <summary>
    /// Resolves the innermost invocation whose argument list encloses
    /// <paramref name="position"/>, along with the zero-based index of the parameter
    /// being written (the number of argument-list commas that precede the position).
    /// </summary>
    public bool TryGetInvocationContext(
        int position,
        out IFunctionSymbol? function,
        out int activeParameterIndex,
        CancellationToken cancellationToken = default
    )
    {
        function = null;
        activeParameterIndex = 0;

        for (
            var current = FindNode(position, cancellationToken);
            current is not null;
            current = current.Parent
        )
        {
            if (
                current is InvocationExpressionSyntax invocation
                && Contains(invocation.ArgumentList, position)
            )
            {
                function =
                    GetSymbolInfo(invocation.Expression, cancellationToken) as IFunctionSymbol;
                activeParameterIndex = CountPrecedingCommas(invocation.ArgumentList, position);
                return function is not null;
            }
        }

        return false;
    }

    private static int CountPrecedingCommas(ArgumentListSyntax argumentList, int position)
    {
        var arguments = argumentList.Arguments;
        var count = 0;
        for (var i = 0; i < arguments.Count; i++)
        {
            var comma = arguments.Get(i).CommaToken;
            if (comma.Kind == SyntaxKind.CommaToken && comma.Span.End <= position)
            {
                count++;
            }
        }

        return count;
    }

    // Speculative bind must not replace or reparse SyntaxTree.
    public bool TryGetSpeculativeSemanticModel(
        int position,
        SyntaxNode speculative,
        out SemanticModel model
    )
    {
        if (speculative is null)
        {
            model = null!;
            return false;
        }

        model = new SemanticModel(Compilation, SyntaxTree, speculative, position);
        return true;
    }

    /// <summary>
    /// Resolves the container type for a member access at <paramref name="position"/>,
    /// i.e. the type of <c>expr</c> in <c>expr.name</c>, whether <c>name</c> is
    /// missing (cursor right after the dot) or partially typed. Returns
    /// <see langword="false"/> when the position is not within a member access.
    /// </summary>
    public bool TryGetMemberAccessContainer(
        int position,
        out ITypeSymbol? container,
        CancellationToken cancellationToken = default
    )
    {
        var node = FindNode(position, cancellationToken);
        var member =
            node as MemberAccessExpressionSyntax
            ?? (
                node is IdentifierNameSyntax identifier
                && identifier.Parent is MemberAccessExpressionSyntax parentMember
                && ReferenceEquals(parentMember.Name, identifier)
                    ? parentMember
                    : null
            );

        if (member is null)
        {
            container = null;
            return false;
        }

        container = ResolveContainerType(member.Expression, cancellationToken);
        return container is not null;
    }

    private ITypeSymbol? ResolveContainerType(
        ExpressionSyntax expression,
        CancellationToken cancellationToken
    )
    {
        var container = TypeOf(GetSymbolInfo(expression, cancellationToken));
        if (container is not null)
        {
            return container;
        }

        var bound = new ExpressionTypeBinder(Compilation, GetScript()).Bind(expression).ResultType;
        return bound.TypeKind == TypeKind.Error ? null : bound;
    }

    private ISymbol? BindMemberAccess(
        MemberAccessExpressionSyntax member,
        CancellationToken cancellationToken
    )
    {
        var container = ResolveContainerType(member.Expression, cancellationToken);
        if (container is null || !TryGetName(member.Name, out var name))
        {
            return null;
        }

        return FindMember(container, name, includeBase: true);
    }

    private ISymbol? LookupName(IdentifierNameSyntax identifier, int position)
    {
        if (!TryGetName(identifier, out var name))
        {
            return null;
        }

        var function = FindContainingFunction(position);
        if (function is not null)
        {
            var locals = function.Locals;
            for (var i = 0; i < locals.Length; i++)
            {
                if (locals[i].Name.Equals(name))
                {
                    return locals[i];
                }
            }

            var parameters = function.Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name.Equals(name))
                {
                    return parameters[i];
                }
            }
        }

        var script = GetScript();
        return script is null ? null : FindMember(script, name, includeBase: true);
    }

    private ISymbol? FindByDeclaration(ISymbol container, SyntaxNode node)
    {
        var members = container.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (DeclarationEquals(member, node))
            {
                return member;
            }

            if (member is IPropertySymbol property)
            {
                if (DeclarationEquals(property.GetMethod, node))
                {
                    return property.GetMethod;
                }

                if (DeclarationEquals(property.SetMethod, node))
                {
                    return property.SetMethod;
                }
            }

            if (member is IStateSymbol or IStructSymbol)
            {
                var nested = FindByDeclaration(member, node);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private ISymbol? FindParameter(ParameterSyntax parameter)
    {
        var owner = parameter.Parent is ParameterListSyntax list ? list.Parent : parameter.Parent;
        if (owner is null || GetDeclaredSymbol(owner) is not IFunctionSymbol function)
        {
            return null;
        }

        if (TryGetName(parameter.Name, out var name))
        {
            var parameters = function.Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name.Equals(name))
                {
                    return parameters[i];
                }
            }
        }

        return null;
    }

    private ISymbol? FindLocal(DeclareStatementSyntax declare)
    {
        var function = FindContainingFunction(declare.Span.Start);
        if (function is null || !TryGetName(declare.Name, out var name))
        {
            return null;
        }

        var locals = function.Locals;
        for (var i = 0; i < locals.Length; i++)
        {
            if (locals[i].Name.Equals(name))
            {
                return locals[i];
            }
        }

        return null;
    }

    private ISymbol? FindNamed(ISymbol container, IdentifierNameSyntax syntax, SymbolKind kind)
    {
        if (!TryGetName(syntax, out var name))
        {
            return null;
        }

        var members = container.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i].Kind == kind)
            {
                return members[i];
            }
        }

        return null;
    }

    private IFunctionSymbol? FindContainingFunction(int position)
    {
        if (!SyntaxTree.TryGetRoot(out var root))
        {
            return null;
        }

        return FindContainingFunction(root.Members, position);
    }

    private IFunctionSymbol? FindContainingFunction(
        SyntaxList<MemberDeclarationSyntax> members,
        int position
    )
    {
        var count = members.Count;
        for (var i = 0; i < count; i++)
        {
            switch (members.Get(i))
            {
                case FunctionDeclarationSyntax function when Contains(function, position):
                    return GetDeclaredSymbol(function) as IFunctionSymbol;
                case EventDeclarationSyntax ev when Contains(ev, position):
                    return GetDeclaredSymbol(ev) as IFunctionSymbol;
                case PropertyDeclarationSyntax property when Contains(property, position):
                    return FindContainingAccessor(property.Accessors, position);
                case StateDeclarationSyntax state when Contains(state, position):
                    return FindContainingFunction(state.Members, position);
                case GroupDeclarationSyntax group when Contains(group, position):
                    var grouped = group.Members;
                    for (var g = 0; g < grouped.Count; g++)
                    {
                        var groupedProperty = grouped.Get(g);
                        if (Contains(groupedProperty, position))
                        {
                            return FindContainingAccessor(groupedProperty.Accessors, position);
                        }
                    }

                    break;
            }
        }

        return null;
    }

    private IFunctionSymbol? FindContainingAccessor(
        SyntaxList<FunctionDeclarationSyntax> accessors,
        int position
    )
    {
        var count = accessors.Count;
        for (var i = 0; i < count; i++)
        {
            var accessor = accessors.Get(i);
            if (Contains(accessor, position))
            {
                return GetDeclaredSymbol(accessor) as IFunctionSymbol;
            }
        }

        return null;
    }

    private IScriptSymbol? GetScript()
    {
        if (_script is not null)
        {
            return _script;
        }

        if (!SyntaxTree.TryGetRoot(out var root) || root.Name.IsMissing)
        {
            return null;
        }

        var name = Compilation.GetInternedQualifiedName(root.Name);
        if (name.Parts.IsDefaultOrEmpty)
        {
            return null;
        }

        _script = Compilation.GetScript(name);
        return _script;
    }

    private int ScopePosition(SyntaxNode node)
    {
        return IsSpeculative(node) ? _position : node.Span.Start;
    }

    private bool IsSpeculative(SyntaxNode node)
    {
        if (_speculative is null)
        {
            return false;
        }

        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _speculative))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetName(IdentifierNameSyntax syntax, out PapyrusName name)
    {
        if (syntax.IsMissing || syntax.Identifier.IsMissing || syntax.Identifier.Text.Length == 0)
        {
            name = default;
            return false;
        }

        name = Compilation.Names.Get(syntax.Identifier.Text);
        return true;
    }

    private static ISymbol? FindMember(ITypeSymbol type, PapyrusName name, bool includeBase)
    {
        var members = type.GetMembers(name);
        if (!members.IsEmpty)
        {
            return members[0];
        }

        if (
            includeBase
            && type is IScriptSymbol script
            && script.BaseType is { TypeKind: not TypeKind.Error } baseType
        )
        {
            return FindMember(baseType, name, includeBase: true);
        }

        return null;
    }

    private static ITypeSymbol? TypeOf(ISymbol? symbol)
    {
        return symbol switch
        {
            ITypeSymbol type => type,
            IFunctionSymbol function => function.ReturnType,
            IPropertySymbol property => property.Type,
            IVariableSymbol variable => variable.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }

    private static bool DeclarationEquals(ISymbol? symbol, SyntaxNode node)
    {
        SyntaxNode? declaration = symbol switch
        {
            FunctionSymbol function => function.Declaration,
            EventSymbol ev => ev.Declaration,
            PropertySymbol property => property.Declaration,
            VariableSymbol variable => variable.Declaration,
            _ => null,
        };

        return declaration is not null
            && declaration.Span == node.Span
            && ReferenceEquals(declaration.SyntaxTree, node.SyntaxTree);
    }

    private static bool IsExtendsName(SyntaxNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is ExtendsClauseSyntax)
            {
                return true;
            }

            if (current is MemberDeclarationSyntax or StatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool Contains(SyntaxNode node, int position)
    {
        var span = node.FullSpan;
        return position >= span.Start && position <= span.End;
    }
}
