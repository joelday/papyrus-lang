using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis;

/// <summary>What <see cref="SemanticModel.GetCompletionContext"/> found at a position.</summary>
public enum CompletionContextKind
{
    None,
    Member,
    Type,
    Statement,
    Declaration,
}

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
        // Only a name refers to a symbol: on a declaration's keywords (`Property`,
        // `Function`, ...) the innermost node is the declaration itself, which is not a
        // reference to it.
        var node = FindNode(position, cancellationToken);
        return node is NameSyntax ? GetSymbolInfo(node, cancellationToken) : null;
    }

    /// <summary>
    /// Resolves the identifier at <paramref name="position"/> for a rename
    /// request. Returns <see langword="false"/> when there is no identifier
    /// there, or the bound symbol cannot be renamed: script (type) symbols
    /// need cross-file import/extends updates plus a physical file rename
    /// (out of scope for now), and native symbols are backed by engine code
    /// the workspace cannot edit (**#75**).
    /// </summary>
    public bool TryGetRenameInfo(
        int position,
        CancellationToken cancellationToken,
        out TextSpan span,
        out ISymbol symbol
    )
    {
        span = default;
        symbol = null!;
        if (FindNode(position, cancellationToken) is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        var candidate = GetSymbolInfo(identifier, cancellationToken);
        if (candidate is null || candidate.Kind == SymbolKind.Script || candidate.IsNative)
        {
            return false;
        }

        span = identifier.Span;
        symbol = candidate;
        return true;
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

    /// <summary>
    /// Finds every reference to <paramref name="symbol"/> within this model's own
    /// tree, including its declaration. Because <see cref="GetSymbolInfo(SyntaxNode,
    /// CancellationToken)"/> resolves an identifier at a declaration site to the
    /// same declared symbol as at a use site, this only needs to walk every
    /// <see cref="IdentifierNameSyntax"/> in the tree and keep the ones that bind to
    /// <paramref name="symbol"/>. Symbols are compared by reference since members are
    /// cached per <see cref="PapyrusCompilation"/>; searching across a different
    /// compilation (e.g. another project) is not supported.
    /// </summary>
    public ImmutableArray<ReferenceLocation> FindReferencesInTree(
        ISymbol symbol,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(symbol);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SyntaxTree.TryGetRoot(out var root))
        {
            return ImmutableArray<ReferenceLocation>.Empty;
        }

        var identifiers = ImmutableArray.CreateBuilder<IdentifierNameSyntax>();
        CollectIdentifierNames(root, identifiers);

        var locations = ImmutableArray.CreateBuilder<ReferenceLocation>();
        var name = symbol.Name.Text;
        for (var i = 0; i < identifiers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identifier = identifiers[i];

            // A reference spells the symbol's name; binding anything else is wasted work.
            if (!identifier.Identifier.Text.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var declared = GetDeclaredSymbol(identifier, cancellationToken);
            if (declared is not null)
            {
                if (SymbolsRefer(declared, symbol))
                {
                    locations.Add(new ReferenceLocation(identifier.Span, IsDeclaration: true));
                }

                continue;
            }

            if (SymbolsRefer(GetSymbolInfo(identifier, cancellationToken), symbol))
            {
                locations.Add(new ReferenceLocation(identifier.Span, IsDeclaration: false));
            }
        }

        return locations.ToImmutable();
    }

    private static bool SymbolsRefer(ISymbol? candidate, ISymbol target)
    {
        return candidate is not null
            && (
                ReferenceEquals(candidate, target)
                || (
                    candidate.Kind == target.Kind
                    && candidate.Name.Equals(target.Name)
                    && ReferenceEquals(candidate.ContainingSymbol, target.ContainingSymbol)
                )
            );
    }

    private static void CollectIdentifierNames(
        SyntaxNode node,
        ImmutableArray<IdentifierNameSyntax>.Builder builder
    )
    {
        CollectNodes(node, builder);
    }

    private static void CollectNodes<T>(SyntaxNode node, ImmutableArray<T>.Builder builder)
        where T : SyntaxNode
    {
        if (node is T match)
        {
            builder.Add(match);
        }

        var offset = node.Position;
        for (var i = 0; i < node.Green.SlotCount; i++)
        {
            var slot = node.Green.GetSlot(i);
            if (slot is null)
            {
                continue;
            }

            if (slot.Kind == SyntaxKind.List)
            {
                var listOffset = offset;
                for (var j = 0; j < slot.SlotCount; j++)
                {
                    var item = slot.GetSlot(j);
                    if (item is not null and not Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken)
                    {
                        CollectNodes(item.CreateRed(node, listOffset), builder);
                    }

                    listOffset += item?.FullWidth ?? 0;
                }
            }
            else if (slot is not Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken)
            {
                CollectNodes(slot.CreateRed(node, offset), builder);
            }

            offset += slot.FullWidth;
        }
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
            case IdentifierNameSyntax identifier
                when identifier.Parent is QualifiedNameSyntax qualified
                    && ReferenceEquals(qualified.Left, identifier)
                    && EnclosingTypeName(identifier) is not null:
                // `Other` in `Other:Point`: the script named by the prefix (null for a
                // namespace that is not itself a script).
                return GetScript() is { } context
                    ? ScriptNamed(Compilation.GetInternedQualifiedName(identifier), context)
                    : null;
            case IdentifierNameSyntax identifier when EnclosingTypeName(identifier) is { } typeName:
                return BindTypeSymbol(typeName);
            case IdentifierNameSyntax identifier when IsInvokedName(identifier):
                return LookupFunction(identifier) ?? LookupName(identifier, ScopePosition(node));
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

    /// <summary>
    /// True when this script directly declares a member named <paramref name="name"/>
    /// of the given <paramref name="kind"/> (as opposed to only inheriting one). Used
    /// by completion to tell an already-overridden event apart from one only offered
    /// by the base type chain, so the latter can be completed as a full override
    /// skeleton built from the symbol's own shape rather than a static snippet.
    /// </summary>
    public bool DeclaresMemberDirectly(PapyrusName name, SymbolKind kind)
    {
        var script = GetScript();
        if (script is null)
        {
            return false;
        }

        foreach (var member in script.GetMembers(name))
        {
            if (member.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddMembersWithBaseTypes(
        ITypeSymbol container,
        ImmutableArray<ISymbol>.Builder builder
    )
    {
        // The most-derived declaration of a name wins (an override hides the base's), states
        // are not referable members, and an Extends cycle is not followed around.
        var seen = new HashSet<(string, SymbolKind)>();
        var visited = new HashSet<ITypeSymbol>(ReferenceEqualityComparer.Instance);
        for (ITypeSymbol? current = container; current is not null && visited.Add(current); )
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IStateSymbol && seen.Add((member.Name.Text.ToUpperInvariant(), member.Kind)))
                {
                    builder.Add(member);
                }
            }

            current = current is IScriptSymbol script ? script.BaseType : null;
        }
    }

    /// <summary>
    /// What kind of completion fits at <paramref name="position"/>: nothing (inside a
    /// comment or string, or after a `.` whose receiver is unknown), the members of
    /// <paramref name="container"/>, a type name, a statement in a function body, or a
    /// script-level declaration.
    /// </summary>
    public CompletionContextKind GetCompletionContext(
        int position,
        out ITypeSymbol? container,
        CancellationToken cancellationToken = default)
    {
        container = null;
        if (IsInCommentOrString(position, cancellationToken))
        {
            return CompletionContextKind.None;
        }

        if (TryGetMemberAccessContainer(position, out container, cancellationToken))
        {
            return CompletionContextKind.Member;
        }

        var text = SyntaxTree.GetText();
        var wordStart = position;
        while (wordStart > 0 && IsWordChar(text.GetChar(wordStart - 1)))
        {
            wordStart--;
        }

        var before = PreviousSignificantPosition(wordStart);
        if (before >= 0 && text.GetChar(before) == '.')
        {
            // A member access whose receiver did not resolve: no sensible suggestions.
            return CompletionContextKind.None;
        }

        if (before >= 0)
        {
            var end = before + 1;
            var start = end;
            while (start > 0 && IsWordChar(text.GetChar(start - 1)))
            {
                start--;
            }

            var previousWord = text.ToString(new TextSpan(start, end - start));
            if (previousWord.Equals("new", StringComparison.OrdinalIgnoreCase)
                || previousWord.Equals("as", StringComparison.OrdinalIgnoreCase)
                || previousWord.Equals("is", StringComparison.OrdinalIgnoreCase))
            {
                return CompletionContextKind.Type;
            }

            if (text.GetChar(before) is '(' or ',' && IsInDeclarationParameterList(before, cancellationToken))
            {
                return CompletionContextKind.Type;
            }
        }

        // A word at the start of a line may be a keyword or a type; both contexts offer both.
        return InFunctionBody(position) ? CompletionContextKind.Statement : CompletionContextKind.Declaration;
    }

    /// <summary>The function, event, or property accessor whose body contains <paramref name="position"/>.</summary>
    public IFunctionSymbol? GetEnclosingFunction(int position) => FindContainingFunction(position);

    /// <summary>
    /// Every call in <paramref name="function"/>'s body that resolves to a function, with the
    /// span of the called name.
    /// </summary>
    public ImmutableArray<(IFunctionSymbol Target, TextSpan Span)> GetOutgoingCalls(
        IFunctionSymbol function,
        CancellationToken cancellationToken = default)
    {
        SyntaxNode? body = function switch
        {
            FunctionSymbol { Declaration: { } syntax } => syntax,
            EventSymbol { Declaration: { } syntax } => syntax,
            _ => null,
        };
        if (body is null || !ReferenceEquals(body.SyntaxTree, SyntaxTree))
        {
            return ImmutableArray<(IFunctionSymbol, TextSpan)>.Empty;
        }

        var invocations = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        CollectNodes(body, invocations);
        var calls = ImmutableArray.CreateBuilder<(IFunctionSymbol, TextSpan)>();
        foreach (var invocation in invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = invocation.Expression switch
            {
                NameExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier,
                MemberAccessExpressionSyntax member => member.Name,
                _ => null,
            };
            if (name is not null && GetSymbolInfo(invocation.Expression, cancellationToken) is IFunctionSymbol target)
            {
                calls.Add((target, name.Span));
            }
        }

        return calls.ToImmutable();
    }

    /// <summary>
    /// What this script's <c>Import</c>s put in scope unqualified: their global functions
    /// and their structs.
    /// </summary>
    public ImmutableArray<ISymbol> LookupImportedSymbols()
    {
        if (GetScript() is not SourceScriptSymbol script)
        {
            return ImmutableArray<ISymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ISymbol>();
        foreach (var import in script.Imports)
        {
            foreach (var member in import.GetMembers())
            {
                if (member is IFunctionSymbol { IsGlobal: true } or IStructSymbol)
                {
                    builder.Add(member);
                }
            }
        }

        return builder.ToImmutable();
    }

    private bool InFunctionBody(int position)
    {
        var function = FindContainingFunction(position);
        if (function is null)
        {
            return false;
        }

        // Past the header line (the body starts after the header's end of line).
        foreach (var reference in function.DeclaringSyntaxReferences)
        {
            var headerEnd = SyntaxTree.GetText().Lines.GetLineEnd(
                SyntaxTree.GetText().Lines.GetPosition(reference.Span.Start).Line);
            return position > headerEnd;
        }

        return false;
    }

    private bool IsInDeclarationParameterList(int position, CancellationToken cancellationToken)
    {
        for (var node = FindNode(position, cancellationToken); node is not null; node = node.Parent)
        {
            if (node is ParameterListSyntax)
            {
                return true;
            }

            if (node is ArgumentListSyntax or StatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private bool IsInCommentOrString(int position, CancellationToken cancellationToken)
    {
        var probe = Math.Max(0, position - 1);
        var spans = Papyrus.CodeAnalysis.Classification.SyntacticClassifier.Classify(
            SyntaxTree,
            new TextSpan(probe, Math.Min(2, Math.Max(1, SyntaxTree.GetText().Length - probe))),
            cancellationToken);
        foreach (var span in spans)
        {
            var inside = span.Kind switch
            {
                Papyrus.CodeAnalysis.Classification.ClassificationKind.Comment
                    => span.Span.Start < position && position <= span.Span.End,
                Papyrus.CodeAnalysis.Classification.ClassificationKind.String
                    => span.Span.Start < position && position < span.Span.End,
                _ => false,
            };
            if (inside)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or ':';

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

        // In an unterminated call (`Wait(` at the end of the line) the node at the cursor is
        // the end of the line, outside the call; the character before it is inside.
        var previous = PreviousSignificantPosition(position);
        var invocation = InvocationAround(FindNode(position, cancellationToken), position)
            ?? (previous >= 0 ? InvocationAround(FindNode(previous, cancellationToken), previous) : null);
        if (invocation is null)
        {
            return false;
        }

        function = GetSymbolInfo(invocation.Expression, cancellationToken) as IFunctionSymbol;
        activeParameterIndex = CountPrecedingCommas(invocation.ArgumentList, position);
        return function is not null;
    }

    // The last non-blank character before the cursor on its line: trailing blanks attach to
    // the end-of-line token, outside the call or member access being typed.
    private int PreviousSignificantPosition(int position)
    {
        var text = SyntaxTree.GetText();
        var probe = Math.Min(position, text.Length) - 1;
        while (probe >= 0 && text.GetChar(probe) is ' ' or '\t')
        {
            probe--;
        }

        return probe >= 0 && text.GetChar(probe) is '\n' or '\r' ? -1 : probe;
    }

    private static InvocationExpressionSyntax? InvocationAround(SyntaxNode? node, int position)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation && Contains(invocation.ArgumentList, position))
            {
                return invocation;
            }
        }

        return null;
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
        // Right after the `.` the name is missing (zero width), so the node at the cursor is
        // whatever follows; the character before the cursor (the `.` or the partly typed
        // name) belongs to the member access.
        var previous = PreviousSignificantPosition(position);
        var member = MemberAccessAt(FindNode(position, cancellationToken))
            ?? (previous >= 0 ? MemberAccessAt(FindNode(previous, cancellationToken)) : null);

        if (member is null)
        {
            container = null;
            return false;
        }

        container = ResolveContainerType(member.Expression, cancellationToken);
        return container is not null;
    }

    private static MemberAccessExpressionSyntax? MemberAccessAt(SyntaxNode? node)
    {
        return node switch
        {
            MemberAccessExpressionSyntax member => member,
            IdentifierNameSyntax identifier
                when identifier.Parent is MemberAccessExpressionSyntax parent && IsSameNode(parent.Name, identifier)
                => parent,
            _ => null,
        };
    }

    /// <summary>
    /// Reference errors in a function, event, or accessor body: names that resolve to
    /// nothing (PAP1013), members missing from a known receiver type (PAP1014), and calls
    /// that resolve to no function (PAP1012). Missing syntax is skipped (the parser already
    /// reported it), and so is a member access whose receiver did not resolve, so one
    /// mistake yields one diagnostic.
    /// </summary>
    internal ImmutableArray<Diagnostic> GetBodyDiagnostics(SyntaxNode body, CancellationToken cancellationToken = default)
    {
        var expressions = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        CollectNodes(body, expressions);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var expression in expressions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (expression)
            {
                case NameExpressionSyntax { Name: IdentifierNameSyntax identifier } name:
                    CheckName(name, identifier, diagnostics, cancellationToken);
                    break;
                case MemberAccessExpressionSyntax member:
                    CheckMemberAccess(member, diagnostics, cancellationToken);
                    break;
            }
        }

        return diagnostics.ToImmutable();
    }

    private void CheckName(
        NameExpressionSyntax name,
        IdentifierNameSyntax identifier,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (identifier.IsMissing || !TryGetName(identifier, out var text))
        {
            return;
        }

        // The VM's state variable, only meaningful (and bound) in GetState/GotoState.
        if (text.Text.Equals("__state", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var symbol = GetSymbolInfo(identifier, cancellationToken);
        var invoked = name.Parent is InvocationExpressionSyntax invocation && IsSameNode(invocation.Expression, name);
        if (invoked && symbol is not IFunctionSymbol)
        {
            diagnostics.Add(new Diagnostic(
                new DiagnosticInfo(DiagnosticCatalog.UnresolvedFunction, text.Text), identifier.Span, SyntaxTree));
        }
        else if (symbol is null)
        {
            diagnostics.Add(new Diagnostic(
                new DiagnosticInfo(DiagnosticCatalog.UndefinedName, text.Text), identifier.Span, SyntaxTree));
        }
    }

    private void CheckMemberAccess(
        MemberAccessExpressionSyntax member,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (member.Name.IsMissing || !TryGetName(member.Name, out var name))
        {
            return;
        }

        var container = ResolveContainerType(member.Expression, cancellationToken);
        if (container is null or { TypeKind: TypeKind.Error })
        {
            return;
        }

        if (container.TypeKind == TypeKind.Array && name.Text.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var invoked = member.Parent is InvocationExpressionSyntax invocation && IsSameNode(invocation.Expression, member);
        if (invoked)
        {
            if (FindFunction(container, name) is null)
            {
                diagnostics.Add(new Diagnostic(
                    new DiagnosticInfo(
                        DiagnosticCatalog.UnresolvedFunction,
                        SymbolDisplay.TypeText(container) + "." + name.Text),
                    member.Name.Span,
                    SyntaxTree));
            }
        }
        else if (FindMember(container, name, includeBase: true) is null)
        {
            diagnostics.Add(new Diagnostic(
                new DiagnosticInfo(DiagnosticCatalog.MemberNotFound, SymbolDisplay.TypeText(container), name.Text),
                member.Name.Span,
                SyntaxTree));
        }
    }

    private ITypeSymbol? ResolveContainerType(
        ExpressionSyntax expression,
        CancellationToken cancellationToken
    )
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return ResolveContainerType(parenthesized.Expression, cancellationToken);
            case ElementAccessExpressionSyntax elementAccess:
                // `arr[i].Member`: the member of the array's element type.
                return ResolveContainerType(elementAccess.Expression, cancellationToken)?.GetArrayElementType();
            case CastExpressionSyntax { OperatorToken.Kind: SyntaxKind.AsKeyword } cast:
                return new ExpressionTypeBinder(Compilation, GetScript()).BindTypeName(cast.Type) is
                    { TypeKind: not TypeKind.Error } castType
                    ? castType
                    : null;
        }

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

        // `x.Name(...)` calls a function even if a property or state shares the name.
        if (member.Parent is InvocationExpressionSyntax invocation && IsSameNode(invocation.Expression, member)
            && FindFunction(container, name) is { } function)
        {
            return function;
        }

        return FindMember(container, name, includeBase: true);
    }

    // The type named by a type position (`Base Property P`, `Base local`, `A:B[]`): its
    // script or struct. Intrinsic types have no declaration to show or go to.
    private ITypeSymbol? BindTypeSymbol(TypeNameSyntax typeName)
    {
        ITypeSymbol? type = new ExpressionTypeBinder(Compilation, GetScript()).BindTypeName(typeName);
        while (type is { TypeKind: TypeKind.Array })
        {
            type = type.GetArrayElementType();
        }

        return type is { TypeKind: TypeKind.Script or TypeKind.Struct } ? type : null;
    }

    private static TypeNameSyntax? EnclosingTypeName(IdentifierNameSyntax identifier)
    {
        SyntaxNode? current = identifier;
        while (current?.Parent is QualifiedNameSyntax)
        {
            current = current.Parent;
        }

        return current?.Parent as TypeNameSyntax;
    }

    // Red nodes are not unique: a parent's child accessor and a walk over its slots can
    // produce different instances for the same syntax, so compare by position and kind.
    private static bool IsSameNode(SyntaxNode left, SyntaxNode right)
        => left.Span == right.Span && left.Kind == right.Kind;

    private static bool IsInvokedName(IdentifierNameSyntax identifier)
        => identifier.Parent is NameExpressionSyntax name
            && name.Parent is InvocationExpressionSyntax invocation
            && IsSameNode(invocation.Expression, name);

    // Calls resolve to functions only (as the compiler binds them): own and inherited
    // members, then imported scripts' globals.
    private IFunctionSymbol? LookupFunction(IdentifierNameSyntax identifier)
    {
        var script = GetScript();
        if (script is null || !TryGetName(identifier, out var name))
        {
            return null;
        }

        return FindFunction(script, name) ?? SourceScriptSymbol.FindImportedGlobalFunction(script, name);
    }

    private static IFunctionSymbol? FindFunction(ITypeSymbol type, PapyrusName name)
    {
        var visited = new HashSet<ITypeSymbol>(ReferenceEqualityComparer.Instance);
        for (ITypeSymbol? current = type; current is not null && current.TypeKind != TypeKind.Error && visited.Add(current);)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IFunctionSymbol function)
                {
                    return function;
                }
            }

            current = current is IScriptSymbol script ? script.BaseType : null;
        }

        return null;
    }

    private ISymbol? LookupName(IdentifierNameSyntax identifier, int position)
    {
        if (!TryGetName(identifier, out var name))
        {
            return null;
        }

        // `Self` is this script and `Parent` its base script (`Self.GetStageDone()`).
        if (identifier.Parent is NameExpressionSyntax)
        {
            if (name.Text.Equals("Self", StringComparison.OrdinalIgnoreCase))
            {
                return GetScript();
            }

            if (name.Text.Equals("Parent", StringComparison.OrdinalIgnoreCase))
            {
                return GetScript()?.BaseType is { TypeKind: not TypeKind.Error } baseType ? baseType : null;
            }
        }

        var function = FindContainingFunction(position);
        if (function is not null)
        {
            if (LocalScopes.Find(function.Locals, name, position) is { } local)
            {
                return local;
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
        if (script is null)
        {
            return null;
        }

        // Then a script named like this (`Base.Twice(...)`, `Game.GetPlayer()`), resolved
        // relative to this script's namespace like any type name.
        return FindMember(script, name, includeBase: true)
            ?? ScriptNamed(new QualifiedName(name), script);
    }

    private IScriptSymbol? ScriptNamed(QualifiedName name, IScriptSymbol context)
    {
        var script = Compilation.GetScriptForType(name, context.QualifiedName);
        return script.TypeKind == TypeKind.Error ? null : script;
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

        return LocalScopes.FindDeclared(function.Locals, declare)
            ?? LocalScopes.Find(function.Locals, name, declare.Span.Start);
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
        => ScriptHierarchy.FindMember(type, name, includeBase);

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

    /// <summary>
    /// Finds every occurrence of the symbol at <paramref name="position"/> within
    /// this model's own tree (declaration and every use site), for
    /// <c>textDocument/documentHighlight</c>. Unlike <see cref="FindReferencesInTree"/>
    /// this resolves the symbol from a position rather than accepting one, since the
    /// LSP request only carries a position.
    /// </summary>
    public ImmutableArray<ReferenceLocation> GetDocumentHighlights(
        int position,
        CancellationToken cancellationToken = default
    )
    {
        var node = FindNode(position, cancellationToken);
        var identifier =
            node as IdentifierNameSyntax
            ?? (node?.Parent as IdentifierNameSyntax);
        if (identifier is null)
        {
            return ImmutableArray<ReferenceLocation>.Empty;
        }

        var symbol =
            GetDeclaredSymbol(identifier, cancellationToken)
            ?? GetSymbolInfo(identifier, cancellationToken);
        return symbol is null
            ? ImmutableArray<ReferenceLocation>.Empty
            : FindReferencesInTree(symbol, cancellationToken);
    }

    /// <summary>
    /// Reports a parameter-name inlay hint for every positional call argument within
    /// <paramref name="span"/> (named arguments already show the name in source, so
    /// they are skipped). Hints are derived from the symbol graph, not a static list.
    /// </summary>
    public ImmutableArray<InlayHint> GetInlayHints(
        TextSpan span,
        CancellationToken cancellationToken = default
    )
    {
        if (!SyntaxTree.TryGetRoot(out var root))
        {
            return ImmutableArray<InlayHint>.Empty;
        }

        var invocations = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        CollectNodes(root, invocations);

        var hints = ImmutableArray.CreateBuilder<InlayHint>();
        for (var i = 0; i < invocations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = invocations[i];
            if (invocation.Span.End < span.Start || invocation.Span.Start > span.End)
            {
                continue;
            }

            if (
                GetSymbolInfo(invocation.Expression, cancellationToken)
                is not IFunctionSymbol function
            )
            {
                continue;
            }

            var arguments = invocation.ArgumentList.Arguments;
            for (var a = 0; a < arguments.Count && a < function.Parameters.Length; a++)
            {
                var argument = arguments.Get(a);
                if (argument.Name is not null)
                {
                    continue;
                }

                hints.Add(
                    new InlayHint(argument.Expression.Span.Start, function.Parameters[a].Name)
                );
            }
        }

        return hints.ToImmutable();
    }
}

/// <summary>
/// A parameter-name inlay hint at <paramref name="Position"/> (the start of the
/// argument expression it labels), from <see
/// cref="SemanticModel.GetInlayHints(TextSpan, CancellationToken)"/>.
/// </summary>
public readonly record struct InlayHint(int Position, PapyrusName ParameterName);

/// <summary>
/// A single reference to a symbol found by <see
/// cref="SemanticModel.FindReferencesInTree(ISymbol, CancellationToken)"/>.
/// </summary>
public readonly record struct ReferenceLocation(TextSpan Span, bool IsDeclaration);

