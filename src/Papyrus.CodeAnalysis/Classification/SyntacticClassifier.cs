using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using GreenToken = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken;
using GreenTrivia = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenTrivia;

namespace Papyrus.CodeAnalysis.Classification;

/// <summary>
/// Classifies tokens and comment trivia from syntax alone. Identifiers are
/// classified only where the tree shape decides their meaning (declaration
/// names, type positions, parameters); identifiers that need symbol
/// information (member accesses, name expressions) are left unclassified.
/// </summary>
public static class SyntacticClassifier
{
    public static ImmutableArray<ClassifiedSpan> Classify(
        SyntaxTree tree,
        TextSpan? span = null,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(tree);
        return ClassifyCore(tree, model: null, span, cancellationToken);
    }

    internal static ImmutableArray<ClassifiedSpan> ClassifyCore(
        SyntaxTree tree,
        SemanticModel? model,
        TextSpan? span,
        CancellationToken cancellationToken)
    {
        var root = tree.GetRoot();
        var filter = span ?? new TextSpan(0, root.FullSpan.Length);
        var builder = ImmutableArray.CreateBuilder<ClassifiedSpan>();
        VisitNode(root, model, filter, builder, cancellationToken);
        return builder.ToImmutable();
    }

    private static void VisitNode(
        SyntaxNode node,
        SemanticModel? model,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var green = node.Green;
        var offset = node.Position;
        for (var i = 0; i < green.SlotCount; i++)
        {
            var slot = green.GetSlot(i);
            if (slot is null)
            {
                continue;
            }

            if (Intersects(filter, offset, slot.FullWidth))
            {
                VisitSlot(node, slot, offset, i, model, filter, builder, cancellationToken);
            }

            offset += slot.FullWidth;
        }
    }

    private static void VisitSlot(
        SyntaxNode parent,
        GreenNode slot,
        int position,
        int slotIndex,
        SemanticModel? model,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (slot is GreenToken)
        {
            ClassifyToken(new SyntaxToken(slot, parent, position), model, filter, builder, cancellationToken);
            return;
        }

        if (slot.Kind == SyntaxKind.List)
        {
            var elementOffset = position;
            for (var i = 0; i < slot.SlotCount; i++)
            {
                var element = slot.GetSlot(i);
                if (element is not null)
                {
                    if (Intersects(filter, elementOffset, element.FullWidth))
                    {
                        // List elements are not direct slots of the parent, so
                        // pass -1 to create their red nodes positionally.
                        VisitSlot(parent, element, elementOffset, -1, model, filter, builder, cancellationToken);
                    }

                    elementOffset += element.FullWidth;
                }
            }

            return;
        }

        var child = slotIndex >= 0
            ? parent.GetRed<SyntaxNode>(slotIndex)
            : slot.CreateRed(parent, position);
        if (child is not null)
        {
            VisitNode(child, model, filter, builder, cancellationToken);
        }
    }

    private static void ClassifyToken(
        SyntaxToken token,
        SemanticModel? model,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        ClassifyTrivia(token.LeadingTrivia, token.Position, filter, builder);

        var span = token.Span;
        if (!token.IsMissing
            && span.Length > 0
            && Intersects(filter, span.Start, span.Length)
            && TryClassifyTokenKind(token, model, out var kind, out var modifiers, cancellationToken))
        {
            builder.Add(new ClassifiedSpan(span, kind, modifiers));
        }

        ClassifyTrivia(token.TrailingTrivia, span.End, filter, builder);
    }

    private static void ClassifyTrivia(
        SyntaxTriviaList trivia,
        int position,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder)
    {
        var node = trivia.Node;
        if (node is null)
        {
            return;
        }

        if (node is GreenTrivia single)
        {
            ClassifySingleTrivia(single, position, filter, builder);
            return;
        }

        for (var i = 0; i < node.SlotCount; i++)
        {
            if (node.GetSlot(i) is GreenTrivia item)
            {
                ClassifySingleTrivia(item, position, filter, builder);
                position += item.FullWidth;
            }
        }
    }

    private static void ClassifySingleTrivia(
        GreenTrivia trivia,
        int position,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder)
    {
        if (trivia.FullWidth == 0 || !Intersects(filter, position, trivia.FullWidth))
        {
            return;
        }

        if (trivia.Kind is SyntaxKind.LineCommentTrivia or SyntaxKind.BlockCommentTrivia)
        {
            builder.Add(new ClassifiedSpan(new TextSpan(position, trivia.FullWidth), ClassificationKind.Comment));
        }
        else if (trivia.Kind == SyntaxKind.SkippedTokensTrivia)
        {
            foreach (var comment in SkippedTextComments.Collect(trivia.Text, position))
            {
                if (Intersects(filter, comment.Span.Start, comment.Span.Length))
                {
                    builder.Add(new ClassifiedSpan(comment.Span, ClassificationKind.Comment));
                }
            }
        }
    }

    private static bool TryClassifyTokenKind(
        SyntaxToken token,
        SemanticModel? model,
        out ClassificationKind kind,
        out ClassificationModifiers modifiers,
        CancellationToken cancellationToken)
    {
        modifiers = ClassificationModifiers.None;
        switch (token.Kind)
        {
            case SyntaxKind.IntegerLiteralToken:
            case SyntaxKind.HexLiteralToken:
            case SyntaxKind.FloatLiteralToken:
                kind = ClassificationKind.Number;
                return true;
            case SyntaxKind.StringLiteralToken:
                kind = ClassificationKind.String;
                return true;
            case SyntaxKind.DocCommentToken:
                kind = ClassificationKind.Comment;
                modifiers = ClassificationModifiers.Documentation;
                return true;
            case SyntaxKind.IdentifierToken:
                if (!TryClassifyIdentifier(token, model, out kind, out var isDeclaration, out var symbol, cancellationToken))
                {
                    return false;
                }

                modifiers = (isDeclaration ? ClassificationModifiers.Declaration : ClassificationModifiers.None)
                    | SymbolModifiers(symbol);
                return true;
            default:
                if (SyntaxFacts.IsOperator(token.Kind))
                {
                    kind = ClassificationKind.Operator;
                    return true;
                }

                if (SyntaxFacts.IsKeyword(token.Kind))
                {
                    kind = ClassificationKind.Keyword;
                    return true;
                }

                kind = default;
                return false;
        }
    }

    private static bool TryClassifyIdentifier(
        SyntaxToken token,
        SemanticModel? model,
        out ClassificationKind kind,
        out bool isDeclaration,
        out ISymbol? symbol,
        CancellationToken cancellationToken)
    {
        kind = default;
        isDeclaration = false;
        symbol = null;
        var parent = token.Parent;
        if (parent is TypeNameSyntax)
        {
            // The struct part of a `Type#Struct` type name.
            kind = ClassificationKind.StructName;
            return true;
        }

        if (parent is FlagSyntax)
        {
            // User-defined .flg flags lex as identifiers but read as the
            // built-in keyword flags beside them.
            kind = ClassificationKind.Keyword;
            return true;
        }

        if (parent is not IdentifierNameSyntax name)
        {
            return false;
        }

        // Walk out of a qualified name (namespaced script or type names).
        SyntaxNode? container = name.Parent;
        while (container is QualifiedNameSyntax)
        {
            container = container.Parent;
        }

        // `__state` is the VM's built-in state variable (ScriptObject.GetState/GotoState).
        if (container is NameExpressionSyntax && token.Text.Equals("__state", System.StringComparison.OrdinalIgnoreCase))
        {
            kind = ClassificationKind.VariableName;
            return true;
        }

        // `Self` and `Parent` read like `this`/`base`.
        if (container is NameExpressionSyntax
            && (token.Text.Equals("Self", System.StringComparison.OrdinalIgnoreCase)
                || token.Text.Equals("Parent", System.StringComparison.OrdinalIgnoreCase)))
        {
            kind = ClassificationKind.Keyword;
            return true;
        }

        if (model is not null)
        {
            // Declarations keep their syntactic kind but pick up modifiers (Global, Const);
            // references and type names take the kind of what they resolve to.
            symbol = container is TypeNameSyntax or ExtendsClauseSyntax
                ? model.GetSymbolInfo(name, cancellationToken)
                : model.GetDeclaredSymbol(container ?? name, cancellationToken)
                    ?? model.GetSymbolInfo(name, cancellationToken);
        }

        switch (container)
        {
            case TypeNameSyntax or ExtendsClauseSyntax:
                kind = symbol is IStructSymbol ? ClassificationKind.StructName : ClassificationKind.TypeName;
                return true;
            case ScriptDeclarationSyntax:
                kind = ClassificationKind.TypeName;
                isDeclaration = true;
                return true;
            case FunctionDeclarationSyntax:
                kind = ClassificationKind.FunctionName;
                isDeclaration = true;
                return true;
            case EventDeclarationSyntax or CustomEventDeclarationSyntax:
                kind = ClassificationKind.EventName;
                isDeclaration = true;
                return true;
            case PropertyDeclarationSyntax:
                kind = ClassificationKind.PropertyName;
                isDeclaration = true;
                return true;
            case VariableDeclarationSyntax or DeclareStatementSyntax or GuardDeclarationSyntax:
                kind = ClassificationKind.VariableName;
                isDeclaration = true;
                return true;
            case LockParameterSyntax:
                kind = ClassificationKind.VariableName;
                return true;
            case StateDeclarationSyntax:
                kind = ClassificationKind.StateName;
                isDeclaration = true;
                return true;
            case StructDeclarationSyntax:
                kind = ClassificationKind.StructName;
                isDeclaration = true;
                return true;
            case GroupDeclarationSyntax:
                kind = ClassificationKind.GroupName;
                isDeclaration = true;
                return true;
            case ParameterSyntax:
                kind = ClassificationKind.ParameterName;
                isDeclaration = true;
                return true;
            case ArgumentSyntax argument when ReferenceEquals(argument.Name, name):
                kind = ClassificationKind.ParameterName;
                return true;
            default:
                return symbol is not null && TryClassifySymbol(symbol, out kind);
        }
    }

    private static bool TryClassifySymbol(ISymbol symbol, out ClassificationKind kind)
    {
        switch (symbol)
        {
            case IEventSymbol or ICustomEventSymbol:
                kind = ClassificationKind.EventName;
                return true;
            case IFunctionSymbol:
                kind = ClassificationKind.FunctionName;
                return true;
            case IPropertySymbol:
                kind = ClassificationKind.PropertyName;
                return true;
            case IParameterSymbol:
                kind = ClassificationKind.ParameterName;
                return true;
            case IVariableSymbol or IGuardSymbol:
                kind = ClassificationKind.VariableName;
                return true;
            case IStructSymbol:
                kind = ClassificationKind.StructName;
                return true;
            case IScriptSymbol:
                kind = ClassificationKind.TypeName;
                return true;
            case IStateSymbol:
                kind = ClassificationKind.StateName;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static ClassificationModifiers SymbolModifiers(ISymbol? symbol)
    {
        return symbol switch
        {
            IFunctionSymbol { IsGlobal: true } => ClassificationModifiers.Static,
            IVariableSymbol { IsConst: true } => ClassificationModifiers.ReadOnly,
            PropertySymbol { Declaration: { } declaration } when IsReadOnly(declaration) => ClassificationModifiers.ReadOnly,
            _ => ClassificationModifiers.None,
        };
    }

    private static bool IsReadOnly(PropertyDeclarationSyntax declaration)
    {
        for (var i = 0; i < declaration.Flags.Count; i++)
        {
            if (declaration.Flags.Get(i).FlagToken.Kind is SyntaxKind.AutoReadOnlyKeyword or SyntaxKind.ConstKeyword)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects(TextSpan filter, int start, int width)
    {
        return start < filter.End && start + width > filter.Start;
    }
}
