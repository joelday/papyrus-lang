using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Emit;

internal sealed class PexEmitter
{
    private readonly FlagsFile? _flagsFile;
    private readonly TimeProvider? _timeProvider;
    private readonly bool _optimize;
    private readonly List<Diagnostic> _diagnostics = new();
    private PapyrusCompilation? _compilation;
    private SyntaxTree? _tree;
    private PexOpCode _maxOpCode;

    /// <param name="optimize">When true, run <see cref="PexOptimizer"/> after emit. Default false.</param>
    public PexEmitter(FlagsFile? flagsFile = null, TimeProvider? timeProvider = null, bool optimize = false)
    {
        _flagsFile = flagsFile;
        _timeProvider = timeProvider;
        _optimize = optimize;
    }

    public ImmutableArray<Diagnostic> Diagnostics => _diagnostics.ToImmutableArray();

    public PexFile EmitFile(IScriptSymbol script, CancellationToken cancellationToken = default)
    {
        var obj = EmitObject(script, cancellationToken);
        var dialect = _compilation?.Dialect ?? PapyrusDialect.SkyrimSe;
        var userFlags = ImmutableArray<PexUserFlag>.Empty;
        if (_flagsFile is { Flags: { IsDefaultOrEmpty: false } flags })
        {
            var builder = ImmutableArray.CreateBuilder<PexUserFlag>(flags.Length);
            for (var i = 0; i < flags.Length; i++)
            {
                var flag = flags[i];
                if (flag.BitIndex is < 0 or > 31)
                {
                    continue;
                }

                builder.Add(new PexUserFlag
                {
                    Name = flag.Name,
                    BitIndex = (byte)flag.BitIndex,
                });
            }

            userFlags = builder.ToImmutable();
        }

        var file = new PexFile
        {
            Version = dialect.PexVersion,
            Game = dialect.Game,
            RawGameId = GameIdMapping.ToRaw(dialect.Game),
            CompileTime = _timeProvider?.GetUtcNow() ?? DateTimeOffset.UnixEpoch,
            SourcePath = script.QualifiedName.ToRelativePath(),
            UserName = "",
            MachineName = "",
            UserFlags = userFlags,
            Objects = ImmutableArray.Create(obj),
        };
        if (_optimize)
        {
            PexOptimizer.Optimize(file);
        }

        return file;
    }

    public PexObject Emit(IScriptSymbol script, CancellationToken cancellationToken = default)
    {
        var obj = EmitObject(script, cancellationToken);
        if (_optimize)
        {
            PexOptimizer.Optimize(obj);
        }

        return obj;
    }

    private PexObject EmitObject(IScriptSymbol script, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(script);
        _diagnostics.Clear();
        _compilation = script is SourceScriptSymbol source ? source.Compilation : null;
        _tree = script.DeclaringSyntaxReferences is { IsDefaultOrEmpty: false } refs
            ? refs[0].SyntaxTree
            : null;
        var dialect = _compilation?.Dialect ?? PapyrusDialect.SkyrimSe;
        _maxOpCode = dialect.MaxOpCode;

        if (_compilation is null)
        {
            ReportInternal("Cannot emit a script that is not backed by a source compilation.");
            return new PexObject
            {
                Name = script.Name.Text,
                States = ImmutableArray.Create(new PexState { Name = "" }),
            };
        }

        var members = script.GetMembers();
        var variables = ImmutableArray.CreateBuilder<PexVariable>();
        var properties = ImmutableArray.CreateBuilder<PexProperty>();
        var structs = ImmutableArray.CreateBuilder<PexStruct>();
        var guards = ImmutableArray.CreateBuilder<PexGuard>();
        var emptyFunctions = ImmutableArray.CreateBuilder<PexFunction>();
        var namedStates = ImmutableArray.CreateBuilder<PexState>();
        var autoStateName = "";

        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (members[i])
            {
                case IVariableSymbol variable:
                    variables.Add(EmitVariable(variable));
                    break;
                case IPropertySymbol property:
                    EmitProperty(property, variables, properties, cancellationToken);
                    break;
                case IFunctionSymbol function:
                    emptyFunctions.Add(EmitFunction(function, cancellationToken));
                    break;
                case IStateSymbol state:
                    if (state.IsAuto && autoStateName.Length == 0)
                    {
                        autoStateName = state.Name.Text;
                    }

                    namedStates.Add(EmitState(state, cancellationToken));
                    break;
                case IStructSymbol struc:
                    structs.Add(EmitStruct(struc));
                    break;
                case IGuardSymbol guard:
                    guards.Add(new PexGuard { Name = guard.Name.Text });
                    break;
            }
        }

        var states = ImmutableArray.CreateBuilder<PexState>(1 + namedStates.Count);
        states.Add(new PexState { Name = "", Functions = emptyFunctions.ToImmutable() });
        states.AddRange(namedStates);

        ScriptDeclarationSyntax? declaration = null;
        if (_tree is not null)
        {
            _tree.TryGetRoot(out declaration);
        }

        var parent = script.BaseType is { TypeKind: not TypeKind.Error } baseType
            ? baseType.QualifiedName.ToString()
            : "";

        var emitted = new PexObject
        {
            Name = script.QualifiedName.ToString(),
            ParentClassName = parent,
            DocumentationString = Documentation(declaration?.DocComment ?? default),
            IsConst = declaration is not null && HasFlag(declaration.Flags, SyntaxKind.ConstKeyword),
            UserFlags = declaration is null ? 0 : UserFlags(declaration.Flags),
            AutoStateName = autoStateName,
            Structs = structs.ToImmutable(),
            Variables = variables.ToImmutable(),
            Guards = guards.ToImmutable(),
            Properties = properties.ToImmutable(),
            States = states.ToImmutable(),
        };
        return emitted;
    }

    private PexState EmitState(IStateSymbol state, CancellationToken cancellationToken)
    {
        var members = state.GetMembers();
        var functions = ImmutableArray.CreateBuilder<PexFunction>();
        for (var i = 0; i < members.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (members[i] is IFunctionSymbol function)
            {
                functions.Add(EmitFunction(function, cancellationToken));
            }
        }

        return new PexState { Name = state.Name.Text, Functions = functions.ToImmutable() };
    }

    private PexStruct EmitStruct(IStructSymbol struc)
    {
        var members = struc.GetMembers();
        var emitted = ImmutableArray.CreateBuilder<PexStructMember>();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IVariableSymbol variable)
            {
                continue;
            }

            emitted.Add(new PexStructMember
            {
                Name = variable.Name.Text,
                TypeName = PexEmitType.NameOf(variable.Type),
                UserFlags = UserFlags(variable),
                DefaultValue = DefaultValue(variable),
                IsConst = variable.IsConst,
                DocumentationString = Documentation(variable is VariableSymbol { Declaration: { } syntax }
                    ? syntax.DocComment
                    : default),
            });
        }

        return new PexStruct { Name = struc.Name.Text, Members = emitted.ToImmutable() };
    }

    private void EmitProperty(
        IPropertySymbol property,
        ImmutableArray<PexVariable>.Builder variables,
        ImmutableArray<PexProperty>.Builder properties,
        CancellationToken cancellationToken)
    {
        var syntax = (property as PropertySymbol)?.Declaration;
        var autoReadOnly = syntax is not null && HasFlag(syntax.Flags, SyntaxKind.AutoReadOnlyKeyword);
        var isAuto = property.IsAuto;
        var emitted = new PexProperty
        {
            Name = property.Name.Text,
            TypeName = PexEmitType.NameOf(property.Type),
            DocumentationString = Documentation(syntax?.DocComment ?? default),
            UserFlags = UserFlags(property),
            IsAuto = isAuto,
            IsReadable = isAuto || property.GetMethod is not null,
            IsWritable = isAuto ? !autoReadOnly : property.SetMethod is not null,
        };

        if (isAuto)
        {
            var backing = "::" + property.Name.Text + "_var";
            emitted.AutoVar = backing;
            variables.Add(new PexVariable
            {
                Name = backing,
                TypeName = emitted.TypeName,
                UserFlags = emitted.UserFlags,
                DefaultValue = DefaultValue(syntax?.EqualsValue),
                IsConst = autoReadOnly,
            });
        }
        else
        {
            if (property.GetMethod is { } getter)
            {
                emitted.ReadFunction = EmitFunction(getter, cancellationToken);
            }

            if (property.SetMethod is { } setter)
            {
                emitted.WriteFunction = EmitFunction(setter, cancellationToken);
            }
        }

        properties.Add(emitted);
    }

    private PexVariable EmitVariable(IVariableSymbol variable)
    {
        return new PexVariable
        {
            Name = variable.Name.Text,
            TypeName = PexEmitType.NameOf(variable.Type),
            UserFlags = UserFlags(variable),
            DefaultValue = DefaultValue(variable),
            IsConst = variable.IsConst,
        };
    }

    private PexFunction EmitFunction(IFunctionSymbol function, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var functionSyntax = (function as FunctionSymbol)?.Declaration;
        var eventSyntax = (function as EventSymbol)?.Declaration;
        var parameters = function.Parameters;
        var parameterBuilder = ImmutableArray.CreateBuilder<PexFunctionParameter>(parameters.Length);
        for (var i = 0; i < parameters.Length; i++)
        {
            parameterBuilder.Add(new PexFunctionParameter
            {
                Name = parameters[i].Name.Text,
                TypeName = PexEmitType.NameOf(parameters[i].Type),
            });
        }

        var emitted = new PexFunction
        {
            Name = function.Name.Text,
            ReturnTypeName = PexEmitType.NameOf(function.ReturnType),
            DocumentationString = Documentation(functionSyntax?.DocComment ?? eventSyntax?.DocComment ?? default),
            UserFlags = UserFlags(function),
            IsNative = function.IsNative,
            IsGlobal = function.IsGlobal,
            Parameters = parameterBuilder.ToImmutable(),
        };

        if (function.IsNative)
        {
            return emitted;
        }

        if (_compilation is null)
        {
            emitted.Instructions = ImmutableArray.Create(new PexInstruction
            {
                OpCode = PexOpCode.Return,
                Arguments = ImmutableArray.Create(PexValue.None),
            });
            return emitted;
        }

        var lowered = Lowering.LowerFunction(Binder.BindFunction(function));
        var graph = ControlFlowGraph.Build(lowered);
        var body = new FunctionBodyEmitter(this, function).Emit(graph, cancellationToken);
        emitted.Locals = body.Locals;
        emitted.Instructions = body.Instructions;
        return emitted;
    }

    private uint UserFlags(ISymbol symbol)
    {
        return symbol switch
        {
            FunctionSymbol { Declaration: { } syntax } => UserFlags(syntax.Flags),
            EventSymbol { Declaration: { } syntax } => UserFlags(syntax.Flags),
            PropertySymbol { Declaration: { } syntax } => UserFlags(syntax.Flags),
            VariableSymbol { Declaration: { } syntax } => UserFlags(syntax.Flags),
            _ => 0,
        };
    }

    private uint UserFlags(SyntaxList<FlagSyntax> flags)
    {
        if (_flagsFile is null || _flagsFile.Flags.IsDefaultOrEmpty)
        {
            return 0;
        }

        uint mask = 0;
        var count = flags.Count;
        for (var i = 0; i < count; i++)
        {
            var token = flags.Get(i).FlagToken;
            if (IsLanguageFlag(token.Kind) || token.Text.Length == 0)
            {
                continue;
            }

            var declared = _flagsFile.Flags;
            for (var f = 0; f < declared.Length; f++)
            {
                var flag = declared[f];
                if (flag.BitIndex is < 0 or > 31)
                {
                    continue;
                }

                if (string.Equals(flag.Name, token.Text, StringComparison.OrdinalIgnoreCase))
                {
                    mask |= 1u << flag.BitIndex;
                    break;
                }
            }
        }

        return mask;
    }

    private PexValue DefaultValue(IVariableSymbol variable)
        => DefaultValue((variable as VariableSymbol)?.Declaration?.EqualsValue);

    private static PexValue DefaultValue(EqualsValueClauseSyntax? clause)
        => clause is null ? PexValue.None : ConstantValue(clause.Value);

    private static PexValue ConstantValue(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                return LiteralValue(literal.Token);
            case PrefixUnaryExpressionSyntax unary:
                var inner = ConstantValue(unary.Operand);
                if (unary.OperatorToken.Kind == SyntaxKind.PlusToken)
                {
                    return inner;
                }

                if (unary.OperatorToken.Kind == SyntaxKind.MinusToken)
                {
                    return Negate(inner);
                }

                return PexValue.None;
            case ParenthesizedExpressionSyntax parenthesized:
                return ConstantValue(parenthesized.Expression);
            default:
                return PexValue.None;
        }
    }

    private static PexValue Negate(PexValue value)
    {
        return value.Type switch
        {
            PexValueType.Integer => PexFunctionBuilder.Integer(unchecked(-value.Integer)),
            PexValueType.Float => PexFunctionBuilder.Float(-value.Float),
            _ => PexValue.None,
        };
    }

    private static PexValue LiteralValue(SyntaxToken token)
    {
        switch (token.Kind)
        {
            case SyntaxKind.IntegerLiteralToken:
                return PexFunctionBuilder.Integer(
                    int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                        ? integer
                        : 0);
            case SyntaxKind.HexLiteralToken:
                var hex = token.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token.Text[2..]
                    : token.Text;
                return PexFunctionBuilder.Integer(
                    int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue)
                        ? hexValue
                        : 0);
            case SyntaxKind.FloatLiteralToken:
                return PexFunctionBuilder.Float(
                    float.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
                        ? floating
                        : 0f);
            case SyntaxKind.StringLiteralToken:
                var text = token.Text;
                if (text.Length >= 2 && text[0] == '"')
                {
                    text = text[^1] == '"' ? text[1..^1] : text[1..];
                }

                return PexFunctionBuilder.String(text);
            case SyntaxKind.TrueKeyword:
                return PexFunctionBuilder.Bool(true);
            case SyntaxKind.FalseKeyword:
                return PexFunctionBuilder.Bool(false);
            default:
                return PexValue.None;
        }
    }

    private static string Documentation(SyntaxToken token)
    {
        var text = token.Text;
        if (text.Length >= 2 && text[0] == '{' && text[^1] == '}')
        {
            return text[1..^1];
        }

        return text;
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

    private static bool IsLanguageFlag(SyntaxKind kind)
        => kind is SyntaxKind.NativeKeyword
            or SyntaxKind.GlobalKeyword
            or SyntaxKind.AutoKeyword
            or SyntaxKind.AutoReadOnlyKeyword
            or SyntaxKind.ConstKeyword
            or SyntaxKind.DebugOnlyKeyword
            or SyntaxKind.BetaOnlyKeyword
            or SyntaxKind.MandatoryKeyword;

    private void ReportInternal(string message)
    {
        _diagnostics.Add(new Diagnostic(
            new DiagnosticInfo(DiagnosticCatalog.InternalError, message),
            new TextSpan(0, 0),
            _tree));
    }

    private void ReportUnsupported(BoundKind kind)
        => ReportInternal("Unsupported bound node '" + kind + "'.");

    private bool CanEmit(PexOpCode op) => (byte)op <= (byte)_maxOpCode;

    private readonly struct Rvalue
    {
        public Rvalue(PexValue value, PexLocal? temp)
        {
            Value = value;
            Temp = temp;
        }

        public PexValue Value { get; }

        public PexLocal? Temp { get; }

        public static Rvalue Immediate(PexValue value) => new(value, temp: null);
    }

    private sealed class FunctionBodyEmitter
    {
        private readonly PexEmitter _parent;
        private readonly IFunctionSymbol _function;
        private readonly PexFunctionBuilder _builder = new();
        private readonly List<(int Instruction, int TargetBlock)> _jumps = new();

        public FunctionBodyEmitter(PexEmitter parent, IFunctionSymbol function)
        {
            _parent = parent;
            _function = function;
        }

        public (ImmutableArray<PexLocalVariable> Locals, ImmutableArray<PexInstruction> Instructions) Emit(
            ControlFlowGraph graph,
            CancellationToken cancellationToken)
        {
            var locals = _function.Locals;
            for (var i = 0; i < locals.Length; i++)
            {
                _builder.AddUserLocal(locals[i].Name.Text, PexEmitType.NameOf(locals[i].Type));
            }

            var blocks = graph.Blocks;
            var starts = new int[blocks.Length];
            for (var i = 0; i < blocks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var block = blocks[i];
                starts[block.Ordinal] = _builder.InstructionCount;
                var statements = block.Statements;
                for (var s = 0; s < statements.Length; s++)
                {
                    EmitStatement(statements[s]);
                }

                EmitTerminator(block, i + 1 < blocks.Length ? blocks[i + 1] : null);
            }

            for (var i = 0; i < _jumps.Count; i++)
            {
                var (instruction, targetBlock) = _jumps[i];
                if ((uint)targetBlock < (uint)starts.Length)
                {
                    _builder.PatchJumpOffset(instruction, starts[targetBlock]);
                }
            }

            return (_builder.GetLocals(), _builder.GetInstructions());
        }

        private void EmitStatement(BoundStatement statement)
        {
            switch (statement)
            {
                case BoundLocalDeclaration declaration:
                    _builder.AddUserLocal(declaration.Local.Name.Text, PexEmitType.NameOf(declaration.Local.Type));
                    if (declaration.Initializer is { } initializer)
                    {
                        Release(EmitExpression(initializer, declaration.Local.Name.Text));
                    }

                    break;
                case BoundAssignment assignment:
                    EmitAssignment(assignment);
                    break;
                case BoundExpressionStatement expression:
                    Release(EmitExpression(expression.Expression));
                    break;
                default:
                    _parent.ReportUnsupported(statement.Kind);
                    break;
            }
        }

        private void EmitAssignment(BoundAssignment assignment)
        {
            switch (assignment.Left)
            {
                case BoundLocal local:
                    Release(EmitExpression(assignment.Right, local.Local.Name.Text));
                    break;
                case BoundParameter parameter:
                    Release(EmitExpression(assignment.Right, parameter.Parameter.Name.Text));
                    break;
                case BoundField field when !IsStructField(field):
                    Release(EmitExpression(assignment.Right, field.Field.Name.Text));
                    break;
                case BoundField field:
                    var structReceiver = EmitExpression(field.Receiver!);
                    var structValue = EmitExpression(assignment.Right);
                    TryEmit(
                        PexOpCode.StructSet,
                        structReceiver.Value,
                        PexFunctionBuilder.String(field.Field.Name.Text),
                        structValue.Value);
                    Release(structReceiver);
                    Release(structValue);
                    break;
                case BoundIndex index:
                    var array = EmitExpression(index.Expression);
                    var elementIndex = EmitExpression(index.Index);
                    var value = EmitExpression(assignment.Right);
                    TryEmit(PexOpCode.ArraySetElement, array.Value, elementIndex.Value, value.Value);
                    Release(array);
                    Release(elementIndex);
                    Release(value);
                    break;
                case BoundProperty property:
                    var receiver = EmitReceiver(property.Receiver);
                    var right = EmitExpression(assignment.Right);
                    TryEmit(
                        PexOpCode.PropSet,
                        PexFunctionBuilder.String(property.Property.Name.Text),
                        receiver.Value,
                        right.Value);
                    Release(receiver);
                    Release(right);
                    break;
                default:
                    _parent.ReportUnsupported(assignment.Left.Kind);
                    Release(EmitExpression(assignment.Right));
                    break;
            }
        }

        private void EmitTerminator(BasicBlock block, BasicBlock? next)
        {
            switch (block.Terminator.Kind)
            {
                case ControlFlowTerminatorKind.Jump:
                    EmitJump(PexOpCode.Jmp, block.Terminator.Destination!);
                    break;
                case ControlFlowTerminatorKind.Conditional:
                    var condition = EmitExpression(block.Terminator.Condition!);
                    EmitJump(PexOpCode.JmpF, block.Terminator.FalseSuccessor!, condition.Value);
                    Release(condition);
                    if (!ReferenceEquals(block.Terminator.TrueSuccessor, next))
                    {
                        EmitJump(PexOpCode.Jmp, block.Terminator.TrueSuccessor!);
                    }

                    break;
                case ControlFlowTerminatorKind.Return:
                    if (block.Terminator.Expression is { } expression)
                    {
                        var value = EmitExpression(expression);
                        TryEmit(PexOpCode.Return, value.Value);
                        Release(value);
                    }
                    else
                    {
                        TryEmit(PexOpCode.Return, PexValue.None);
                    }

                    break;
                default:
                    TryEmit(PexOpCode.Return, PexValue.None);
                    break;
            }
        }

        private void EmitJump(PexOpCode op, BasicBlock target, PexValue? condition = null)
        {
            int instruction;
            if (op == PexOpCode.Jmp)
            {
                instruction = TryEmit(op, PexFunctionBuilder.Integer(0));
            }
            else
            {
                instruction = TryEmit(op, condition ?? PexValue.None, PexFunctionBuilder.Integer(0));
            }

            if (instruction >= 0)
            {
                _jumps.Add((instruction, target.Ordinal));
            }
        }

        private Rvalue EmitExpression(BoundExpression expression, string? destName = null)
        {
            switch (expression)
            {
                case BoundLiteral literal:
                    return EmitLeaf(Literal(literal), destName);
                case BoundLocal local:
                    return EmitLeaf(PexFunctionBuilder.Identifier(local.Local.Name.Text), destName);
                case BoundParameter parameter:
                    return EmitLeaf(PexFunctionBuilder.Identifier(parameter.Parameter.Name.Text), destName);
                case BoundField field:
                    return EmitField(field, destName);
                case BoundSelf:
                    return EmitLeaf(PexFunctionBuilder.Identifier("self"), destName);
                case BoundParent:
                    return EmitLeaf(PexFunctionBuilder.Identifier("parent"), destName);
                case BoundTypeExpression type:
                    return EmitLeaf(PexFunctionBuilder.Identifier(PexEmitType.NameOf(type.Type)), destName);
                case BoundErrorExpression:
                    return EmitLeaf(PexValue.None, destName);
                case BoundBinary binary:
                    return EmitBinary(binary, destName);
                case BoundUnary unary:
                    return EmitUnary(unary, destName);
                case BoundConversion conversion:
                    return EmitConversion(conversion, destName);
                case BoundCall call:
                    return EmitCall(call, destName);
                case BoundProperty property:
                    return EmitPropertyGet(property, destName);
                case BoundIndex index:
                    return EmitIndex(index, destName);
                case BoundIs isExpr:
                    return EmitIs(isExpr, destName);
                case BoundNewArray array:
                    return EmitNewArray(array, destName);
                case BoundNew created:
                    return EmitNew(created, destName);
                case BoundArrayLength length:
                    return EmitArrayLength(length, destName);
                case BoundMemberAccess member:
                    return EmitMemberAccess(member, destName);
                default:
                    _parent.ReportUnsupported(expression.Kind);
                    return EmitLeaf(PexValue.None, destName);
            }
        }

        private Rvalue EmitBinary(BoundBinary binary, string? destName)
        {
            switch (binary.OperatorKind)
            {
                case SyntaxKind.AmpersandAmpersandToken:
                    return EmitShortCircuit(binary, destName, jumpIfTrue: false);
                case SyntaxKind.BarBarToken:
                    return EmitShortCircuit(binary, destName, jumpIfTrue: true);
            }

            var left = EmitExpression(binary.Left);
            var right = EmitExpression(binary.Right);
            if (binary.OperatorKind == SyntaxKind.PlusToken && PexEmitType.IsNamed(binary.Type, "String"))
            {
                left = EnsureString(left);
                right = EnsureString(right);
            }

            var dest = AcquireDest(binary.Type, destName, out var allocated);
            if (binary.OperatorKind == SyntaxKind.ExclamationEqualsToken)
            {
                TryEmit(PexOpCode.CmpEq, dest, left.Value, right.Value);
                TryEmit(PexOpCode.Not, dest, dest);
            }
            else if (TryBinaryOp(binary, out var op))
            {
                TryEmit(op, dest, left.Value, right.Value);
            }
            else
            {
                _parent.ReportUnsupported(binary.Kind);
                TryEmit(PexOpCode.Assign, dest, left.Value);
            }

            Release(left);
            Release(right);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitShortCircuit(BoundBinary binary, string? destName, bool jumpIfTrue)
        {
            var dest = AcquireDest(binary.Type, destName, out var allocated);
            var left = EmitExpression(binary.Left, dest.Text);
            var op = jumpIfTrue ? PexOpCode.JmpT : PexOpCode.JmpF;
            var jump = TryEmit(op, dest, PexFunctionBuilder.Integer(0));
            if (left.Temp is not null && left.Temp.Name != dest.Text)
            {
                Release(left);
            }

            var right = EmitExpression(binary.Right, dest.Text);
            if (right.Temp is not null && right.Temp.Name != dest.Text)
            {
                Release(right);
            }

            if (jump >= 0)
            {
                _builder.PatchJumpOffset(jump, _builder.InstructionCount);
            }

            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitUnary(BoundUnary unary, string? destName)
        {
            if (unary.OperatorKind == SyntaxKind.PlusToken)
            {
                return EmitExpression(unary.Operand, destName);
            }

            var operand = EmitExpression(unary.Operand);
            var dest = AcquireDest(unary.Type, destName, out var allocated);
            PexOpCode op;
            switch (unary.OperatorKind)
            {
                case SyntaxKind.ExclamationToken:
                    op = PexOpCode.Not;
                    break;
                case SyntaxKind.MinusToken:
                    op = PexEmitType.IsNamed(unary.Type, "Float") ? PexOpCode.FNeg : PexOpCode.INeg;
                    break;
                default:
                    _parent.ReportUnsupported(unary.Kind);
                    Release(operand);
                    return new Rvalue(operand.Value, allocated);
            }

            TryEmit(op, dest, operand.Value);
            Release(operand);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitConversion(BoundConversion conversion, string? destName)
        {
            if (conversion.ConversionKind == ConversionKind.Identity
                || PexEmitType.NameOf(conversion.Operand.Type) == PexEmitType.NameOf(conversion.Type))
            {
                return EmitExpression(conversion.Operand, destName);
            }

            var operand = EmitExpression(conversion.Operand);
            var dest = AcquireDest(conversion.Type, destName, out var allocated);
            TryEmit(PexOpCode.Cast, dest, operand.Value);
            Release(operand);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitCall(BoundCall call, string? destName)
        {
            var receiver = call.Receiver is null ? Rvalue.Immediate(PexValue.None) : EmitExpression(call.Receiver);
            var arguments = new Rvalue[call.Arguments.Length];
            var values = new PexValue[call.Arguments.Length];
            for (var i = 0; i < call.Arguments.Length; i++)
            {
                arguments[i] = EmitExpression(call.Arguments[i]);
                values[i] = arguments[i].Value;
            }

            if (call.Method?.ContainingSymbol is ITypeSymbol { TypeKind: TypeKind.Array })
            {
                var result = EmitArrayIntrinsic(call, receiver, values, destName);
                Release(receiver);
                for (var i = 0; i < arguments.Length; i++)
                {
                    Release(arguments[i]);
                }

                return result;
            }

            var dest = AcquireDest(call.Type, destName, out var allocated);
            var name = PexFunctionBuilder.String(call.Method?.Name.Text ?? "");
            if (call.Receiver is BoundParent)
            {
                EmitVariadic(PexOpCode.CallParent, Concat(name, dest), values);
            }
            else if (call.Method is { IsGlobal: true } || call.Receiver is BoundTypeExpression)
            {
                var typeName = call.Receiver is BoundTypeExpression type
                    ? PexEmitType.NameOf(type.Type)
                    : call.Method?.ContainingScript is { } owner
                        ? owner.QualifiedName.ToString()
                        : _function.ContainingScript?.QualifiedName.ToString() ?? "";
                EmitVariadic(
                    PexOpCode.CallStatic,
                    Concat(PexFunctionBuilder.String(typeName), name, dest),
                    values);
            }
            else
            {
                var target = call.Receiver is null
                    ? PexFunctionBuilder.Identifier("self")
                    : receiver.Value;
                EmitVariadic(PexOpCode.CallMethod, Concat(name, target, dest), values);
            }

            Release(receiver);
            for (var i = 0; i < arguments.Length; i++)
            {
                Release(arguments[i]);
            }

            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitArrayIntrinsic(BoundCall call, Rvalue array, PexValue[] arguments, string? destName)
        {
            var name = call.Method?.Name.Text ?? "";
            if (name.Equals("Find", StringComparison.OrdinalIgnoreCase)
                || name.Equals("RFind", StringComparison.OrdinalIgnoreCase))
            {
                var dest = AcquireDest(call.Type, destName, out var allocated);
                var op = name.Equals("RFind", StringComparison.OrdinalIgnoreCase)
                    ? PexOpCode.ArrayRFindElement
                    : PexOpCode.ArrayFindElement;
                TryEmit(
                    op,
                    array.Value,
                    dest,
                    ArgumentOr(arguments, 0, PexValue.None),
                    ArgumentOr(arguments, 1, PexFunctionBuilder.Integer(0)));
                return new Rvalue(dest, allocated);
            }

            if (name.Equals("FindStruct", StringComparison.OrdinalIgnoreCase)
                || name.Equals("RFindStruct", StringComparison.OrdinalIgnoreCase))
            {
                var dest = AcquireDest(call.Type, destName, out var allocated);
                var op = name.Equals("RFindStruct", StringComparison.OrdinalIgnoreCase)
                    ? PexOpCode.ArrayRFindStruct
                    : PexOpCode.ArrayFindStruct;
                TryEmit(
                    op,
                    array.Value,
                    dest,
                    ArgumentOr(arguments, 0, PexValue.None),
                    ArgumentOr(arguments, 1, PexValue.None),
                    ArgumentOr(arguments, 2, PexFunctionBuilder.Integer(0)));
                return new Rvalue(dest, allocated);
            }

            if (name.Equals("Add", StringComparison.OrdinalIgnoreCase))
            {
                TryEmit(
                    PexOpCode.ArrayAdd,
                    array.Value,
                    ArgumentOr(arguments, 0, PexValue.None),
                    ArgumentOr(arguments, 1, PexFunctionBuilder.Integer(1)));
                return Rvalue.Immediate(PexValue.None);
            }

            if (name.Equals("Insert", StringComparison.OrdinalIgnoreCase))
            {
                TryEmit(
                    PexOpCode.ArrayInsert,
                    array.Value,
                    ArgumentOr(arguments, 0, PexValue.None),
                    ArgumentOr(arguments, 1, PexFunctionBuilder.Integer(0)));
                return Rvalue.Immediate(PexValue.None);
            }

            if (name.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                TryEmit(
                    PexOpCode.ArrayRemove,
                    array.Value,
                    ArgumentOr(arguments, 0, PexValue.None),
                    ArgumentOr(arguments, 1, PexFunctionBuilder.Integer(1)));
                return Rvalue.Immediate(PexValue.None);
            }

            if (name.Equals("RemoveLast", StringComparison.OrdinalIgnoreCase))
            {
                TryEmit(PexOpCode.ArrayRemoveLast, array.Value);
                return Rvalue.Immediate(PexValue.None);
            }

            if (name.Equals("Clear", StringComparison.OrdinalIgnoreCase))
            {
                TryEmit(PexOpCode.ArrayClear, array.Value);
                return Rvalue.Immediate(PexValue.None);
            }

            _parent.ReportUnsupported(call.Kind);
            return EmitLeaf(PexValue.None, destName);
        }

        private static PexValue ArgumentOr(PexValue[] arguments, int index, PexValue fallback)
            => (uint)index < (uint)arguments.Length ? arguments[index] : fallback;

        private Rvalue EmitField(BoundField field, string? destName)
        {
            if (!IsStructField(field))
            {
                return EmitLeaf(PexFunctionBuilder.Identifier(field.Field.Name.Text), destName);
            }

            var receiver = EmitExpression(field.Receiver!);
            var dest = AcquireDest(field.Type, destName, out var allocated);
            TryEmit(
                PexOpCode.StructGet,
                dest,
                receiver.Value,
                PexFunctionBuilder.String(field.Field.Name.Text));
            Release(receiver);
            return new Rvalue(dest, allocated);
        }

        private static bool IsStructField(BoundField field)
            => field.Receiver is not null && field.Receiver is not BoundSelf;

        private Rvalue EmitPropertyGet(BoundProperty property, string? destName)
        {
            var receiver = EmitReceiver(property.Receiver);
            var dest = AcquireDest(property.Type, destName, out var allocated);
            TryEmit(
                PexOpCode.PropGet,
                PexFunctionBuilder.String(property.Property.Name.Text),
                receiver.Value,
                dest);
            Release(receiver);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitIndex(BoundIndex index, string? destName)
        {
            var array = EmitExpression(index.Expression);
            var elementIndex = EmitExpression(index.Index);
            var dest = AcquireDest(index.Type, destName, out var allocated);
            TryEmit(PexOpCode.ArrayGetElement, dest, array.Value, elementIndex.Value);
            Release(array);
            Release(elementIndex);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitIs(BoundIs isExpr, string? destName)
        {
            var operand = EmitExpression(isExpr.Operand);
            var dest = AcquireDest(isExpr.Type, destName, out var allocated);
            TryEmit(
                PexOpCode.Is,
                dest,
                operand.Value,
                PexFunctionBuilder.Identifier(PexEmitType.NameOf(isExpr.TestType)));
            Release(operand);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitNewArray(BoundNewArray array, string? destName)
        {
            var size = EmitExpression(array.Size);
            var dest = AcquireDest(array.Type, destName, out var allocated);
            TryEmit(PexOpCode.ArrayCreate, dest, size.Value);
            Release(size);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitNew(BoundNew created, string? destName)
        {
            var dest = AcquireDest(created.Type, destName, out var allocated);
            TryEmit(PexOpCode.StructCreate, dest);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitArrayLength(BoundArrayLength length, string? destName)
        {
            var array = EmitExpression(length.Array);
            var dest = AcquireDest(length.Type, destName, out var allocated);
            TryEmit(PexOpCode.ArrayLength, dest, array.Value);
            Release(array);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitMemberAccess(BoundMemberAccess member, string? destName)
        {
            var receiver = EmitExpression(member.Receiver);
            var dest = AcquireDest(member.Type, destName, out var allocated);
            if (member.Member is IVariableSymbol field)
            {
                TryEmit(
                    PexOpCode.StructGet,
                    dest,
                    receiver.Value,
                    PexFunctionBuilder.String(field.Name.Text));
            }
            else if (member.Member is IPropertySymbol property)
            {
                TryEmit(
                    PexOpCode.PropGet,
                    PexFunctionBuilder.String(property.Name.Text),
                    receiver.Value,
                    dest);
            }
            else
            {
                _parent.ReportUnsupported(member.Kind);
                TryEmit(PexOpCode.Assign, dest, PexValue.None);
            }

            Release(receiver);
            return new Rvalue(dest, allocated);
        }

        private Rvalue EmitReceiver(BoundExpression? receiver)
        {
            if (receiver is null or BoundSelf or BoundParent)
            {
                return Rvalue.Immediate(PexFunctionBuilder.Identifier("self"));
            }

            return EmitExpression(receiver);
        }

        private Rvalue EmitLeaf(PexValue value, string? destName)
        {
            if (destName is null)
            {
                return Rvalue.Immediate(value);
            }

            var dest = PexFunctionBuilder.Identifier(destName);
            TryEmit(PexOpCode.Assign, dest, value);
            return Rvalue.Immediate(dest);
        }

        private Rvalue EnsureString(Rvalue value)
        {
            if (value.Value.Type == PexValueType.String)
            {
                return value;
            }

            var names = _parent._compilation?.Names;
            if (names is null)
            {
                return value;
            }

            var temp = _builder.AllocateTemp(IntrinsicTypeSymbol.Create(names, "String"));
            var dest = PexFunctionBuilder.Identifier(temp.Name);
            TryEmit(PexOpCode.Cast, dest, value.Value);
            Release(value);
            return new Rvalue(dest, temp);
        }

        private PexValue AcquireDest(ITypeSymbol type, string? destName, out PexLocal? allocated)
        {
            if (destName is not null)
            {
                allocated = null;
                return PexFunctionBuilder.Identifier(destName);
            }

            allocated = _builder.AllocateTemp(type);
            return PexFunctionBuilder.Identifier(allocated.Name);
        }

        private void Release(Rvalue value)
        {
            if (value.Temp is not null)
            {
                _builder.ReleaseTemp(value.Temp);
            }
        }

        private bool TryBinaryOp(BoundBinary binary, out PexOpCode op)
        {
            var isFloat = PexEmitType.IsNamed(binary.Type, "Float")
                || PexEmitType.IsNamed(binary.Left.Type, "Float")
                || PexEmitType.IsNamed(binary.Right.Type, "Float");
            switch (binary.OperatorKind)
            {
                case SyntaxKind.PlusToken:
                    op = PexEmitType.IsNamed(binary.Type, "String")
                        ? PexOpCode.StrCat
                        : isFloat ? PexOpCode.FAdd : PexOpCode.IAdd;
                    return true;
                case SyntaxKind.MinusToken:
                    op = isFloat ? PexOpCode.FSub : PexOpCode.ISub;
                    return true;
                case SyntaxKind.AsteriskToken:
                    op = isFloat ? PexOpCode.FMul : PexOpCode.IMul;
                    return true;
                case SyntaxKind.SlashToken:
                    op = isFloat ? PexOpCode.FDiv : PexOpCode.IDiv;
                    return true;
                case SyntaxKind.PercentToken:
                    op = PexOpCode.IMod;
                    return true;
                case SyntaxKind.EqualsEqualsToken:
                    op = PexOpCode.CmpEq;
                    return true;
                case SyntaxKind.LessThanToken:
                    op = PexOpCode.CmpLt;
                    return true;
                case SyntaxKind.LessThanEqualsToken:
                    op = PexOpCode.CmpLte;
                    return true;
                case SyntaxKind.GreaterThanToken:
                    op = PexOpCode.CmpGt;
                    return true;
                case SyntaxKind.GreaterThanEqualsToken:
                    op = PexOpCode.CmpGte;
                    return true;
                default:
                    op = PexOpCode.Nop;
                    return false;
            }
        }

        private static PexValue Literal(BoundLiteral literal)
        {
            switch (literal.Value)
            {
                case int integer:
                    return PexFunctionBuilder.Integer(integer);
                case float floating:
                    return PexFunctionBuilder.Float(floating);
                case bool boolean:
                    return PexFunctionBuilder.Bool(boolean);
                case string text:
                    return PexFunctionBuilder.String(text);
                default:
                    return PexValue.None;
            }
        }

        private int TryEmit(PexOpCode op, params PexValue[] operands)
        {
            if (!_parent.CanEmit(op))
            {
                _parent.ReportInternal("Opcode '" + op + "' is not available in this dialect.");
                return -1;
            }

            return _builder.Emit(op, operands);
        }

        private void EmitVariadic(PexOpCode op, PexValue[] fixedArgs, PexValue[] variadic)
        {
            var operands = new PexValue[fixedArgs.Length + variadic.Length];
            for (var i = 0; i < fixedArgs.Length; i++)
            {
                operands[i] = fixedArgs[i];
            }

            for (var i = 0; i < variadic.Length; i++)
            {
                operands[fixedArgs.Length + i] = variadic[i];
            }

            TryEmit(op, operands);
        }

        private static PexValue[] Concat(params PexValue[] values) => values;
    }
}
