using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class SourceScriptSymbol : MemberOwnerSymbol, IScriptSymbol
{
    [ThreadStatic]
    private static HashSet<(SourceScriptSymbol Symbol, CompletionPart Part)>? t_completing;

    [ThreadStatic]
    private static List<SourceScriptSymbol>? t_completingBaseTypes;

    private readonly PapyrusCompilation _compilation;
    private readonly SyntaxTree _tree;
    private ImmutableArray<IScriptSymbol> _imports;
    private readonly ImmutableArray<SyntaxReference> _declaringSyntaxReferences;
    private readonly ITypeSymbol _unboundType;
    private readonly CompletionSlot _members = new();
    private readonly CompletionSlot _baseTypeSlot = new();
    private readonly CompletionSlot _signatures = new();
    private readonly CompletionSlot _bodies = new();
    private IScriptSymbol? _baseType;
    private ImmutableArray<Diagnostic> _bindingDiagnostics = ImmutableArray<Diagnostic>.Empty;
    private bool _cyclicBase;

    public SourceScriptSymbol(
        PapyrusCompilation compilation,
        SyntaxTree tree,
        PapyrusName name,
        QualifiedName qualifiedName,
        TypeName typeName,
        ImmutableArray<SyntaxReference> declaringSyntaxReferences,
        bool isNative)
        : base(name, SymbolKind.Script, containingSymbol: null, containingScript: null, isNative)
    {
        _compilation = compilation;
        _tree = tree;
        QualifiedName = qualifiedName;
        TypeName = typeName;
        _declaringSyntaxReferences = declaringSyntaxReferences.IsDefault
            ? ImmutableArray<SyntaxReference>.Empty
            : declaringSyntaxReferences;
        _unboundType = IntrinsicTypeSymbol.Create(compilation.Names, "None");
    }

    internal PapyrusCompilation Compilation => _compilation;

    public override IScriptSymbol? ContainingScript => this;

    public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => _declaringSyntaxReferences;

    public QualifiedName QualifiedName { get; }

    public TypeName TypeName { get; }

    public TypeKind TypeKind => TypeKind.Script;

    public IScriptSymbol? BaseType
    {
        get
        {
            ForceComplete(CompletionPart.BaseType, CancellationToken.None);
            return _baseType;
        }
    }

    public ITypeSymbol? GetArrayElementType() => null;

    public override ImmutableArray<ISymbol> GetMembers()
    {
        ForceComplete(CompletionPart.Members, CancellationToken.None);
        return base.GetMembers();
    }

    public override ImmutableArray<ISymbol> GetMembers(PapyrusName name)
    {
        ForceComplete(CompletionPart.Members, CancellationToken.None);
        return base.GetMembers(name);
    }

    internal ImmutableArray<Diagnostic> GetBindingDiagnostics() => _bindingDiagnostics;

    /// <summary>
    /// The scripts named by this script's <c>Import</c> statements, in order. Their
    /// structs and global functions are in scope unqualified (after this script's own
    /// members and its parents'), as in Caprica's <c>importedNodes</c> lookups.
    /// Unresolvable imports are skipped.
    /// </summary>
    internal ImmutableArray<IScriptSymbol> Imports
    {
        get
        {
            if (_imports.IsDefault)
            {
                ImmutableInterlocked.InterlockedInitialize(ref _imports, BindImports());
            }

            return _imports;
        }
    }

    private ImmutableArray<IScriptSymbol> BindImports()
    {
        if (!_tree.TryGetRoot(out var root))
        {
            return ImmutableArray<IScriptSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<IScriptSymbol>();
        for (var i = 0; i < root.Imports.Count; i++)
        {
            AddImport(root.Imports.Get(i), builder);
        }

        // Imports after the header parse as members, in source order.
        for (var i = 0; i < root.Members.Count; i++)
        {
            if (root.Members.Get(i) is ImportDeclarationSyntax import)
            {
                AddImport(import, builder);
            }
        }

        return builder.ToImmutable();
    }

    private void AddImport(ImportDeclarationSyntax import, ImmutableArray<IScriptSymbol>.Builder builder)
    {
        if (import.Name.Name.IsMissing)
        {
            return;
        }

        var name = _compilation.GetInternedTypeName(import.Name).Name;
        var script = _compilation.GetScriptForType(name, QualifiedName);
        if (script.TypeKind != TypeKind.Error)
        {
            builder.Add(script);
        }
    }

    /// <summary>A struct declared directly in one of <paramref name="script"/>'s imports.</summary>
    internal static IStructSymbol? FindImportedStruct(IScriptSymbol? script, PapyrusName name)
    {
        if (script is not SourceScriptSymbol source)
        {
            return null;
        }

        foreach (var import in source.Imports)
        {
            foreach (var member in import.GetMembers())
            {
                if (member is IStructSymbol struc && struc.Name.Equals(name))
                {
                    return struc;
                }
            }
        }

        return null;
    }

    /// <summary>A global function declared directly in one of <paramref name="script"/>'s imports.</summary>
    internal static IFunctionSymbol? FindImportedGlobalFunction(IScriptSymbol? script, PapyrusName name)
    {
        if (script is not SourceScriptSymbol source)
        {
            return null;
        }

        foreach (var import in source.Imports)
        {
            foreach (var member in import.GetMembers(name))
            {
                if (member is IFunctionSymbol { IsGlobal: true } function)
                {
                    return function;
                }
            }
        }

        return null;
    }

    internal void ForceComplete(CompletionPart part, CancellationToken cancellationToken)
    {
        if ((part & (CompletionPart.Members | CompletionPart.MemberSignatures | CompletionPart.MemberBodies)) != 0)
        {
            CompletePart(CompletionPart.Members, _members, ComputeMembers, cancellationToken);
        }

        if ((part & CompletionPart.BaseType) != 0)
        {
            CompletePart(CompletionPart.BaseType, _baseTypeSlot, ComputeBaseType, cancellationToken);
        }

        if ((part & (CompletionPart.MemberSignatures | CompletionPart.MemberBodies)) != 0)
        {
            CompletePart(CompletionPart.MemberSignatures, _signatures, ComputeSignatures, cancellationToken);
        }

        if ((part & CompletionPart.MemberBodies) != 0)
        {
            CompletePart(CompletionPart.MemberBodies, _bodies, ComputeBodies, cancellationToken);
        }
    }

    private void CompletePart(
        CompletionPart part,
        CompletionSlot slot,
        Action<CancellationToken> compute,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (slot.IsFinished)
        {
            return;
        }

        if (!TryEnterCompleting(part))
        {
            OnReentered(part);
            return;
        }

        try
        {
            if (slot.TryStart())
            {
                var error = false;
                try
                {
                    compute(cancellationToken);
                }
                catch
                {
                    error = true;
                    throw;
                }
                finally
                {
                    slot.Finish(error);
                }

                return;
            }
        }
        finally
        {
            LeaveCompleting(part);
        }

        if (slot.IsFinished)
        {
            return;
        }

        slot.Wait(cancellationToken);
    }

    private bool TryEnterCompleting(CompletionPart part)
    {
        var set = t_completing ??= new HashSet<(SourceScriptSymbol, CompletionPart)>();
        return set.Add((this, part));
    }

    private void LeaveCompleting(CompletionPart part)
    {
        t_completing?.Remove((this, part));
    }

    private void OnReentered(CompletionPart part)
    {
        // Mutual field types re-enter MemberSignatures legally; only extends is a cycle.
        if (part != CompletionPart.BaseType)
        {
            return;
        }

        var stack = t_completingBaseTypes;
        if (stack is null)
        {
            _cyclicBase = true;
            return;
        }

        var cycleIndex = stack.IndexOf(this);
        if (cycleIndex < 0)
        {
            _cyclicBase = true;
            return;
        }

        for (var i = cycleIndex; i < stack.Count; i++)
        {
            stack[i]._cyclicBase = true;
        }
    }

    private void ComputeMembers(CancellationToken cancellationToken)
    {
        var members = BindMembers(cancellationToken);
        SetMembers(members);
        CheckDuplicates(members, "script");
        foreach (var member in members)
        {
            if (member is StateSymbol or StructSymbol)
            {
                CheckDuplicates(member.GetMembers(), member is StateSymbol ? "state" : "struct");
            }
        }
    }

    // Functions/events share one name space, properties/variables another; structs,
    // states, and custom events each their own.
    private void CheckDuplicates(ImmutableArray<ISymbol> members, string container)
    {
        var seen = new HashSet<(string, string)>();
        foreach (var member in members)
        {
            var group = member switch
            {
                IFunctionSymbol => "function",
                IPropertySymbol or IVariableSymbol => "variable",
                IStructSymbol => "struct",
                IStateSymbol => "state",
                ICustomEventSymbol => "custom event",
                _ => null,
            };
            if (group is not null
                && !seen.Add((member.Name.Text.ToUpperInvariant(), group))
                && NameSpan(member) is { } span)
            {
                AddDiagnostic(DiagnosticCatalog.DuplicateDeclaration, span, member.Name.Text, container);
            }
        }
    }

    private static TextSpan? NameSpan(ISymbol symbol)
    {
        return symbol switch
        {
            FunctionSymbol { Declaration: { } syntax } => syntax.Name.Span,
            EventSymbol { Declaration: { } syntax } => syntax.Name.Span,
            PropertySymbol { Declaration: { } syntax } => syntax.Name.Span,
            VariableSymbol { Declaration: { } syntax } => syntax.Name.Span,
            StateSymbol { Declaration: { } syntax } => syntax.Name.Span,
            _ => symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty ? null : symbol.DeclaringSyntaxReferences[0].Span,
        };
    }

    private void ComputeBaseType(CancellationToken cancellationToken)
    {
        var stack = t_completingBaseTypes ??= new List<SourceScriptSymbol>();
        stack.Add(this);
        try
        {
            BindBaseType(cancellationToken);
        }
        finally
        {
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private void ComputeSignatures(CancellationToken cancellationToken)
    {
        BindMemberSignatures(cancellationToken);
    }

    private void ComputeBodies(CancellationToken cancellationToken)
    {
        BindMemberBodies(cancellationToken);
    }

    private ImmutableArray<ISymbol> BindMembers(CancellationToken cancellationToken)
    {
        AddDiagnostics(DialectBinding.Collect(_tree, _compilation.Dialect));
        if (!_tree.TryGetRoot(out var root))
        {
            return ImmutableArray<ISymbol>.Empty;
        }

        var members = root.Members;
        var builder = ImmutableArray.CreateBuilder<ISymbol>(members.Count);
        var count = members.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddScriptMember(members.Get(i), builder, cancellationToken);
        }

        return builder.ToImmutable();
    }

    private void AddScriptMember(
        MemberDeclarationSyntax member,
        ImmutableArray<ISymbol>.Builder builder,
        CancellationToken cancellationToken)
    {
        switch (member)
        {
            case FunctionDeclarationSyntax function:
                AddFunction(function, this, builder);
                break;
            case EventDeclarationSyntax ev:
                AddEvent(ev, this, builder);
                break;
            case PropertyDeclarationSyntax property:
                AddProperty(property, this, builder);
                break;
            case VariableDeclarationSyntax variable:
                AddVariable(variable, this, builder);
                break;
            case StateDeclarationSyntax state:
                AddState(state, builder, cancellationToken);
                break;
            case StructDeclarationSyntax struc:
                AddStruct(struc, builder, cancellationToken);
                break;
            case GroupDeclarationSyntax group:
                AddGroup(group, builder, cancellationToken);
                break;
            case CustomEventDeclarationSyntax customEvent:
                AddCustomEvent(customEvent, builder);
                break;
            case GuardDeclarationSyntax guard:
                AddGuard(guard, builder);
                break;
        }
    }

    private void AddFunction(
        FunctionDeclarationSyntax syntax,
        ISymbol container,
        ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        builder.Add(new FunctionSymbol(
            name,
            container,
            this,
            _unboundType,
            isGlobal: HasFlag(syntax.Flags, SyntaxKind.GlobalKeyword),
            isNative: HasFlag(syntax.Flags, SyntaxKind.NativeKeyword),
            syntax));
    }

    private void AddEvent(
        EventDeclarationSyntax syntax,
        ISymbol container,
        ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        if (syntax.RemoteScript is { IsMissing: false } remoteScript)
        {
            // Remote and custom event handlers compile to `::remote_<Script>_<Event>`
            // (Caprica's PapyrusParser::parseFunction), with the script name as written.
            var script = _compilation.GetInternedQualifiedName(remoteScript);
            name = _compilation.Names.Get("::remote_" + script + "_" + name.Text);
        }
        else
        {
            CheckReservedMemberName(name, syntax.Name.Span);
        }

        builder.Add(new EventSymbol(
            name,
            container,
            this,
            _unboundType,
            isNative: HasFlag(syntax.Flags, SyntaxKind.NativeKeyword),
            syntax));
    }

    private void AddProperty(
        PropertyDeclarationSyntax syntax,
        ISymbol container,
        ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        builder.Add(new PropertySymbol(
            name,
            container,
            this,
            _unboundType,
            isAuto: HasFlag(syntax.Flags, SyntaxKind.AutoKeyword)
                || HasFlag(syntax.Flags, SyntaxKind.AutoReadOnlyKeyword),
            syntax));
    }

    private void AddVariable(
        VariableDeclarationSyntax syntax,
        ISymbol container,
        ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        builder.Add(new VariableSymbol(
            name,
            container,
            this,
            _unboundType,
            isConst: HasFlag(syntax.Flags, SyntaxKind.ConstKeyword),
            syntax));
    }

    private void AddState(
        StateDeclarationSyntax syntax,
        ImmutableArray<ISymbol>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        var state = new StateSymbol(
            name,
            this,
            isAuto: syntax.AutoKeyword.Kind == SyntaxKind.AutoKeyword,
            syntax);
        var members = syntax.Members;
        var stateMembers = ImmutableArray.CreateBuilder<ISymbol>(members.Count);
        var count = members.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (members.Get(i))
            {
                case FunctionDeclarationSyntax function:
                    AddFunction(function, state, stateMembers);
                    break;
                case EventDeclarationSyntax ev:
                    AddEvent(ev, state, stateMembers);
                    break;
            }
        }

        state.SetMembers(stateMembers.ToImmutable());
        builder.Add(state);
    }

    private void AddStruct(
        StructDeclarationSyntax syntax,
        ImmutableArray<ISymbol>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        var struc = new StructSymbol(name, this, new TypeName(QualifiedName, isArray: false, name));
        var members = syntax.Members;
        var structMembers = ImmutableArray.CreateBuilder<ISymbol>(members.Count);
        var count = members.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddVariable(members.Get(i), struc, structMembers);
        }

        struc.SetMembers(structMembers.ToImmutable());
        builder.Add(struc);
    }

    private void AddGroup(
        GroupDeclarationSyntax syntax,
        ImmutableArray<ISymbol>.Builder builder,
        CancellationToken cancellationToken)
    {
        var members = syntax.Members;
        var count = members.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddProperty(members.Get(i), this, builder);
        }
    }

    private void AddCustomEvent(CustomEventDeclarationSyntax syntax, ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        builder.Add(new CustomEventSymbol(name, this));
    }

    private void AddGuard(GuardDeclarationSyntax syntax, ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        CheckReservedMemberName(name, syntax.Name.Span);
        builder.Add(new GuardSymbol(name, this, syntax));
    }

    private void BindMemberSignatures(CancellationToken cancellationToken)
    {
        var members = base.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BindMemberSignature(members[i], cancellationToken);
        }
    }

    private void BindMemberSignature(ISymbol member, CancellationToken cancellationToken)
    {
        switch (member)
        {
            case FunctionSymbol function when function.Declaration is { } syntax:
                BindFunctionSignature(function, syntax, cancellationToken);
                break;
            case EventSymbol ev when ev.Declaration is { } syntax:
                BindEventSignature(ev, syntax, cancellationToken);
                break;
            case PropertySymbol property when property.Declaration is { } syntax:
                BindPropertySignature(property, syntax, cancellationToken);
                break;
            case VariableSymbol variable when variable.Declaration is { } syntax:
                variable.Type = BindType(syntax.TypeName, syntax.TypeName.Span);
                break;
            case StateSymbol state:
                BindChildSignatures(state.GetMembers(), cancellationToken);
                break;
            case StructSymbol struc:
                BindChildSignatures(struc.GetMembers(), cancellationToken);
                break;
        }
    }

    private void BindChildSignatures(ImmutableArray<ISymbol> members, CancellationToken cancellationToken)
    {
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BindMemberSignature(members[i], cancellationToken);
        }
    }

    private void BindFunctionSignature(
        FunctionSymbol function,
        FunctionDeclarationSyntax syntax,
        CancellationToken cancellationToken)
    {
        function.ReturnType = syntax.ReturnType is null
            ? _unboundType
            : BindType(syntax.ReturnType, syntax.ReturnType.Span);
        function.SetParameters(BindParameters(function, syntax.ParameterList, cancellationToken));
    }

    private void BindEventSignature(
        EventSymbol ev,
        EventDeclarationSyntax syntax,
        CancellationToken cancellationToken)
    {
        ev.ReturnType = _unboundType;
        ev.SetParameters(BindParameters(ev, syntax.ParameterList, cancellationToken));
    }

    private void BindPropertySignature(
        PropertySymbol property,
        PropertyDeclarationSyntax syntax,
        CancellationToken cancellationToken)
    {
        property.Type = BindType(syntax.TypeName, syntax.TypeName.Span);
        if (property.IsAuto)
        {
            return;
        }

        var accessors = syntax.Accessors;
        var count = accessors.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accessor = accessors.Get(i);
            if (!TryGetName(accessor.Name, out var name))
            {
                continue;
            }

            var function = new FunctionSymbol(
                name,
                property,
                this,
                _unboundType,
                isGlobal: false,
                isNative: HasFlag(accessor.Flags, SyntaxKind.NativeKeyword),
                accessor);
            BindFunctionSignature(function, accessor, cancellationToken);
            if (name.Equals(_compilation.Names.Get("Get")))
            {
                property.GetMethod = function;
            }
            else if (name.Equals(_compilation.Names.Get("Set")))
            {
                property.SetMethod = function;
            }
        }
    }

    private ImmutableArray<IParameterSymbol> BindParameters(
        ISymbol container,
        ParameterListSyntax list,
        CancellationToken cancellationToken)
    {
        var parameters = list.Parameters;
        var count = parameters.Count;
        if (count == 0)
        {
            return ImmutableArray<IParameterSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<IParameterSymbol>(count);
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameter = parameters.Get(i);
            if (!TryGetName(parameter.Name, out var name))
            {
                continue;
            }

            builder.Add(new ParameterSymbol(
                name,
                container,
                this,
                BindType(parameter.TypeName, parameter.TypeName.Span),
                parameter.EqualsValue is not null));
        }

        return builder.ToImmutable();
    }

    private void BindMemberBodies(CancellationToken cancellationToken)
    {
        var members = base.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BindMemberBody(members[i], cancellationToken);
        }

        // With every body's locals bound, check references inside the bodies.
        var model = _compilation.GetSemanticModel(_tree);
        for (var i = 0; i < members.Length; i++)
        {
            CheckMemberBody(model, members[i], cancellationToken);
        }
    }

    private void CheckMemberBody(SemanticModel model, ISymbol member, CancellationToken cancellationToken)
    {
        switch (member)
        {
            case FunctionSymbol { Declaration: { } syntax } function:
                AddDiagnostics(model.GetBodyDiagnostics(syntax, cancellationToken));
                CheckBoundBody(function, syntax.Name);
                break;
            case EventSymbol { Declaration: { } syntax } ev:
                AddDiagnostics(model.GetBodyDiagnostics(syntax, cancellationToken));
                CheckBoundBody(ev, syntax.Name);
                break;
            case PropertySymbol property:
                if (property.GetMethod is { } getMethod)
                {
                    CheckMemberBody(model, getMethod, cancellationToken);
                }

                if (property.SetMethod is { } setMethod)
                {
                    CheckMemberBody(model, setMethod, cancellationToken);
                }

                break;
            case StateSymbol state:
                foreach (var stateMember in state.GetMembers())
                {
                    CheckMemberBody(model, stateMember, cancellationToken);
                }

                break;
        }
    }

    private void BindMemberBody(ISymbol member, CancellationToken cancellationToken)
    {
        switch (member)
        {
            case FunctionSymbol function when function.Declaration is { } syntax:
                function.SetLocals(BindLocals(function, syntax.Statements, cancellationToken));
                break;
            case EventSymbol ev when ev.Declaration is { } syntax:
                ev.SetLocals(BindLocals(ev, syntax.Statements, cancellationToken));
                break;
            case PropertySymbol property:
                if (property.GetMethod is FunctionSymbol getMethod)
                {
                    BindMemberBody(getMethod, cancellationToken);
                }

                if (property.SetMethod is FunctionSymbol setMethod)
                {
                    BindMemberBody(setMethod, cancellationToken);
                }

                break;
            case StateSymbol state:
                var stateMembers = state.GetMembers();
                for (var i = 0; i < stateMembers.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BindMemberBody(stateMembers[i], cancellationToken);
                }

                break;
        }
    }

    // Type, argument, and read-only errors come from binding the body; a function with a
    // return type whose end is reachable gets a warning.
    private void CheckBoundBody(IFunctionSymbol function, IdentifierNameSyntax name)
    {
        if (function.IsNative)
        {
            return;
        }

        // A local redeclared while an earlier one of that name is still in scope.
        var locals = function.Locals;
        for (var i = 0; i < locals.Length; i++)
        {
            if (locals[i] is not VariableSymbol { LocalDeclaration: { } declaration })
            {
                continue;
            }

            for (var j = 0; j < i; j++)
            {
                if (locals[j] is VariableSymbol { LocalDeclaration: { } earlier }
                    && locals[j].Name.Equals(locals[i].Name)
                    && LocalScopes.InScope(earlier, declaration.Span.Start))
                {
                    AddDiagnostic(DiagnosticCatalog.DuplicateDeclaration, declaration.Name.Span, locals[i].Name.Text, "scope");
                    break;
                }
            }
        }

        var builder = new Binding.BoundTreeBuilder(_compilation, this, function) { ReportDiagnostics = true };
        var bound = builder.BindFunction(function);
        AddDiagnostics(builder.Diagnostics);

        if (function.ReturnType is { TypeKind: not TypeKind.Error } returnType
            && !(returnType.TypeKind == TypeKind.Intrinsic && returnType.Name.Text.Equals("None", StringComparison.OrdinalIgnoreCase))
            && EndIsReachable(Emit.ControlFlowGraph.Build(bound)))
        {
            AddDiagnostic(DiagnosticCatalog.NotAllPathsReturn, name.Span, function.Name.Text);
        }
    }

    private static bool EndIsReachable(Emit.ControlFlowGraph graph)
    {
        var visited = new HashSet<Emit.BasicBlock>();
        var pending = new Stack<Emit.BasicBlock>();
        pending.Push(graph.Entry);
        while (pending.Count > 0)
        {
            var block = pending.Pop();
            if (!visited.Add(block))
            {
                continue;
            }

            if (block.Terminator.Kind == Emit.ControlFlowTerminatorKind.FallThrough)
            {
                return true;
            }

            foreach (var successor in block.Terminator.Successors)
            {
                pending.Push(successor);
            }
        }

        return false;
    }

    private ImmutableArray<IVariableSymbol> BindLocals(
        ISymbol container,
        SyntaxList<StatementSyntax> statements,
        CancellationToken cancellationToken)
    {
        var builder = ImmutableArray.CreateBuilder<IVariableSymbol>();
        CollectLocals(container, statements, builder, cancellationToken);
        return builder.ToImmutable();
    }

    private static string? UniqueLocalName(string name, ImmutableArray<IVariableSymbol>.Builder earlier)
    {
        bool Taken(string candidate)
        {
            foreach (var local in earlier)
            {
                if (string.Equals(((VariableSymbol)local).EmittedName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        if (!Taken(name))
        {
            return null;
        }

        for (var i = 0; ; i++)
        {
            var candidate = "::mangled_" + name + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!Taken(candidate))
            {
                return candidate;
            }
        }
    }

    private void CollectLocals(
        ISymbol container,
        SyntaxList<StatementSyntax> statements,
        ImmutableArray<IVariableSymbol>.Builder builder,
        CancellationToken cancellationToken)
    {
        var count = statements.Count;
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (statements.Get(i))
            {
                case DeclareStatementSyntax declare:
                    if (TryGetName(declare.Name, out var name))
                    {
                        builder.Add(new VariableSymbol(
                            name,
                            container,
                            this,
                            BindType(declare.TypeName, declare.TypeName.Span),
                            isConst: HasFlag(declare.Flags, SyntaxKind.ConstKeyword))
                        {
                            LocalDeclaration = declare,
                            _emittedName = UniqueLocalName(name.Text, builder),
                        });
                    }

                    break;
                case IfStatementSyntax ifStatement:
                    CollectLocals(container, ifStatement.Statements, builder, cancellationToken);
                    var elseIfs = ifStatement.ElseIfClauses;
                    for (var e = 0; e < elseIfs.Count; e++)
                    {
                        CollectLocals(container, elseIfs.Get(e).Statements, builder, cancellationToken);
                    }

                    if (ifStatement.ElseClause is { } elseClause)
                    {
                        CollectLocals(container, elseClause.Statements, builder, cancellationToken);
                    }

                    break;
                case WhileStatementSyntax whileStatement:
                    CollectLocals(container, whileStatement.Statements, builder, cancellationToken);
                    break;
                case GuardStatementSyntax guard:
                    CollectLocals(container, guard.Statements, builder, cancellationToken);
                    break;
                case TryGuardStatementSyntax tryGuard:
                    CollectLocals(container, tryGuard.Statements, builder, cancellationToken);
                    break;
            }
        }
    }

    private ITypeSymbol BindType(TypeNameSyntax syntax, TextSpan span)
    {
        if (syntax.Name.IsMissing)
        {
            // The parser already reported the missing type (PAP0001); don't pile on.
            return CreateError(default);
        }

        return BindType(_compilation.GetInternedTypeName(syntax), syntax.Span);
    }

    private ITypeSymbol BindType(TypeName typeName, TextSpan span)
    {
        if (typeName.IsArray)
        {
            var element = BindType(new TypeName(typeName.Name, isArray: false, typeName.StructName), span);
            return new ArrayTypeSymbol(element, _compilation.Names, _compilation.Dialect);
        }

        if (TryGetIntrinsic(typeName, out var intrinsic))
        {
            return intrinsic;
        }

        if (typeName.StructName is { } structName)
        {
            return BindStructType(typeName.Name, structName, span);
        }

        if (typeName.Name.Parts.Length == 1)
        {
            var localStruct = FindStruct(this, typeName.Name.Parts[0])
                ?? FindImportedStruct(this, typeName.Name.Parts[0]);
            if (localStruct is not null)
            {
                return localStruct;
            }
        }

        if (typeName.Name.Parts.IsDefaultOrEmpty)
        {
            // Missing syntax; the parser reported it.
            return CreateError(typeName.Name);
        }

        var script = typeName.Name.Equals(QualifiedName)
            ? this
            : _compilation.GetScriptForType(typeName.Name, QualifiedName);
        if (script.TypeKind == TypeKind.Error
            && ScriptHierarchy.TrySplitStructName(typeName.Name, out var owner, out var ownerStruct))
        {
            // `Script:Struct` when no script is named `Script:Struct` (Caprica's tryFindType).
            var ownerScript = owner.Equals(QualifiedName) ? this : _compilation.GetScriptForType(owner, QualifiedName);
            if (ownerScript.TypeKind != TypeKind.Error && FindStruct(ownerScript, ownerStruct) is { } qualified)
            {
                return qualified;
            }
        }

        if (script.TypeKind == TypeKind.Error)
        {
            AddDiagnostic(DiagnosticCatalog.UnresolvedType, span, typeName);
            return script;
        }

        return script;
    }

    private ITypeSymbol BindStructType(QualifiedName scriptName, PapyrusName structName, TextSpan span)
    {
        IScriptSymbol script;
        if (scriptName.Parts.IsDefaultOrEmpty || scriptName.Equals(QualifiedName))
        {
            script = this;
        }
        else
        {
            script = _compilation.GetScriptForType(scriptName, QualifiedName);
        }

        if (script.TypeKind != TypeKind.Error)
        {
            var struc = FindStruct(script, structName);
            if (struc is not null)
            {
                return struc;
            }
        }

        AddDiagnostic(
            DiagnosticCatalog.UnresolvedType,
            span,
            new TypeName(scriptName, isArray: false, structName));
        return CreateError(scriptName, structName);
    }

    private IStructSymbol? FindStruct(IScriptSymbol script, PapyrusName structName)
    {
        // Structs declared in a parent script are in scope in the child, unqualified
        // or qualified by the child's name (Caprica's tryResolveStruct).
        return ScriptHierarchy.FindStruct(
            script,
            structName,
            s => ReferenceEquals(s, this) ? GetDeclaredMembers() : s.GetMembers());
    }

    private ImmutableArray<ISymbol> GetDeclaredMembers() => base.GetMembers();

    private bool TryGetIntrinsic(TypeName typeName, out ITypeSymbol intrinsic)
    {
        intrinsic = null!;
        if (typeName.IsArray || typeName.StructName is not null || typeName.Name.Parts.Length != 1)
        {
            return false;
        }

        var text = typeName.Name.Parts[0].Text;
        if (!IntrinsicTypeNames.IsIntrinsic(text, _compilation.Dialect))
        {
            return false;
        }

        intrinsic = IntrinsicTypeSymbol.Create(_compilation.Names, text);
        return true;
    }

    private void BindBaseType(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_tree.TryGetRoot(out var root) || root.ExtendsClause is not { } extends)
        {
            _baseType = null;
            return;
        }

        var baseName = _compilation.GetInternedQualifiedName(extends.TypeName.Name);
        var span = extends.TypeName.Name.IsMissing ? extends.Span : extends.TypeName.Span;

        if (_cyclicBase)
        {
            SetCyclicBase(baseName, span);
            return;
        }

        if (baseName.Parts.IsDefaultOrEmpty)
        {
            // `Extends` with no name: the parser reported it.
            _baseType = CreateError(baseName);
            return;
        }

        // Members-only completion, like any type reference: fully completing the base here
        // would run its body checks, which can reach this script's still-binding base type.
        var resolved = _compilation.GetScriptForType(baseName, QualifiedName);

        // Walk the rest of the Extends chain (only the base-type part) so a cycle back to
        // this script is detected here.
        if (resolved is SourceScriptSymbol baseSource)
        {
            baseSource.ForceComplete(CompletionPart.BaseType, cancellationToken);
        }

        if (_cyclicBase)
        {
            SetCyclicBase(baseName, span);
            return;
        }

        if (resolved.TypeKind == TypeKind.Error)
        {
            SetMissingBase(baseName, span, resolved);
            return;
        }

        _baseType = resolved;
    }

    private void SetMissingBase(QualifiedName baseName, TextSpan span, IScriptSymbol? resolved = null)
    {
        _baseType = resolved ?? CreateError(baseName);
        AddDiagnostic(DiagnosticCatalog.UnresolvedBaseType, span, baseName);
    }

    private void SetCyclicBase(QualifiedName baseName, TextSpan span)
    {
        _baseType = CreateError(baseName);
        AddDiagnostic(DiagnosticCatalog.CyclicBaseType, span, QualifiedName);
    }

    private static ScriptSymbol CreateError(QualifiedName name, PapyrusName? structName = null)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        var simple = structName ?? (parts.IsEmpty ? PapyrusName.Empty : parts[^1]);
        return new ScriptSymbol(simple, name, new TypeName(name, isArray: false, structName), isError: true);
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments)
    {
        AddDiagnostic(new Diagnostic(new DiagnosticInfo(descriptor, arguments), span, _tree));
    }

    private void AddDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(
            ref _bindingDiagnostics,
            static (existing, added) => existing.AddRange(added),
            diagnostics);
    }

    private void AddDiagnostic(Diagnostic diagnostic)
    {
        ImmutableInterlocked.Update(ref _bindingDiagnostics, static (existing, d) => existing.Add(d), diagnostic);
    }

    private void CheckReservedMemberName(PapyrusName name, TextSpan span)
    {
        if (name.Equals(_compilation.Names.Get("Self")) || name.Equals(_compilation.Names.Get("Parent")))
        {
            AddDiagnostic(DiagnosticCatalog.ReservedMemberName, span, name);
        }
    }

    private bool TryGetName(IdentifierNameSyntax syntax, out PapyrusName name)
    {
        if (syntax.IsMissing || syntax.Identifier.IsMissing || syntax.Identifier.Text.Length == 0)
        {
            name = default;
            return false;
        }

        name = _compilation.Names.Get(syntax.Identifier.Text);
        return true;
    }

    private static bool HasFlag(SyntaxList<FlagSyntax> flags, SyntaxKind kind)
    {
        var count = flags.Count;
        for (var i = 0; i < count; i++)
        {
            if (flags.Get(i).FlagToken.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
