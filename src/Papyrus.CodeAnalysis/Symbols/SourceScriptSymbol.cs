using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Symbols;

internal sealed class SourceScriptSymbol : MemberOwnerSymbol, IScriptSymbol
{
    private const int NotStarted = 0;
    private const int InProgress = 1;
    private const int Completed = 2;

    [ThreadStatic]
    private static List<SourceScriptSymbol>? t_completingBaseTypes;

    private readonly PapyrusCompilation _compilation;
    private readonly SyntaxTree _tree;
    private readonly ImmutableArray<SyntaxReference> _declaringSyntaxReferences;
    private readonly ITypeSymbol _unboundType;
    private IScriptSymbol? _baseType;
    private ImmutableArray<Diagnostic> _bindingDiagnostics = ImmutableArray<Diagnostic>.Empty;
    private int _membersState;
    private int _baseTypeState;
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

    internal void ForceComplete(CompletionPart part, CancellationToken cancellationToken)
    {
        if ((part & CompletionPart.Members) != 0)
        {
            CompleteMembers(cancellationToken);
        }

        if ((part & CompletionPart.BaseType) != 0)
        {
            CompleteBaseType(cancellationToken);
        }
    }

    private void CompleteMembers(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _membersState) == Completed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _membersState, InProgress, NotStarted) != NotStarted)
        {
            return;
        }

        try
        {
            SetMembers(BindMembers(cancellationToken));
        }
        finally
        {
            Interlocked.Exchange(ref _membersState, Completed);
        }
    }

    private void CompleteBaseType(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _baseTypeState) == Completed)
        {
            return;
        }

        var stack = t_completingBaseTypes ??= new List<SourceScriptSymbol>();
        var cycleIndex = stack.IndexOf(this);
        if (cycleIndex >= 0)
        {
            for (var i = cycleIndex; i < stack.Count; i++)
            {
                stack[i]._cyclicBase = true;
            }

            return;
        }

        if (Interlocked.CompareExchange(ref _baseTypeState, InProgress, NotStarted) != NotStarted)
        {
            return;
        }

        stack.Add(this);
        try
        {
            BindBaseType(cancellationToken);
        }
        finally
        {
            stack.RemoveAt(stack.Count - 1);
            Interlocked.Exchange(ref _baseTypeState, Completed);
        }
    }

    private ImmutableArray<ISymbol> BindMembers(CancellationToken cancellationToken)
    {
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

        builder.Add(new FunctionSymbol(
            name,
            container,
            this,
            _unboundType,
            isGlobal: HasFlag(syntax.Flags, SyntaxKind.GlobalKeyword),
            isNative: HasFlag(syntax.Flags, SyntaxKind.NativeKeyword)));
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

        builder.Add(new EventSymbol(
            name,
            container,
            this,
            _unboundType,
            isNative: HasFlag(syntax.Flags, SyntaxKind.NativeKeyword)));
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

        builder.Add(new PropertySymbol(
            name,
            container,
            this,
            _unboundType,
            isAuto: HasFlag(syntax.Flags, SyntaxKind.AutoKeyword)
                || HasFlag(syntax.Flags, SyntaxKind.AutoReadOnlyKeyword)));
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

        builder.Add(new VariableSymbol(
            name,
            container,
            this,
            _unboundType,
            isConst: HasFlag(syntax.Flags, SyntaxKind.ConstKeyword)));
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

        var state = new StateSymbol(name, this, isAuto: syntax.AutoKeyword.Kind == SyntaxKind.AutoKeyword);
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

        builder.Add(new CustomEventSymbol(name, this));
    }

    private void AddGuard(GuardDeclarationSyntax syntax, ImmutableArray<ISymbol>.Builder builder)
    {
        if (!TryGetName(syntax.Name, out var name))
        {
            return;
        }

        builder.Add(new GuardSymbol(name, this));
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
            SetMissingBase(baseName, span);
            return;
        }

        var resolved = _compilation.GetScript(baseName);
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
        AddDiagnostic(
            PapyrusCompilation.UnresolvedBaseTypeDiagnosticId,
            "Base type '" + baseName + "' could not be resolved.",
            span);
    }

    private void SetCyclicBase(QualifiedName baseName, TextSpan span)
    {
        _baseType = CreateError(baseName);
        AddDiagnostic(
            PapyrusCompilation.CyclicBaseTypeDiagnosticId,
            "Script '" + QualifiedName + "' has a cyclic inheritance chain.",
            span);
    }

    private ScriptSymbol CreateError(QualifiedName name)
    {
        var parts = name.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : name.Parts;
        var simple = parts.IsEmpty ? PapyrusName.Empty : parts[^1];
        return new ScriptSymbol(simple, name, new TypeName(name), isError: true);
    }

    private void AddDiagnostic(string id, string message, TextSpan span)
    {
        _bindingDiagnostics = _bindingDiagnostics.Add(new Diagnostic(new DiagnosticInfo(id, message), span, _tree));
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
