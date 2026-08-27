using System.Collections.Immutable;
using System.Threading;
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
        var root = tree.GetRoot();
        var filter = span ?? new TextSpan(0, root.FullSpan.Length);
        var builder = ImmutableArray.CreateBuilder<ClassifiedSpan>();
        VisitNode(root, filter, builder, cancellationToken);
        return builder.ToImmutable();
    }

    private static void VisitNode(
        SyntaxNode node,
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
                VisitSlot(node, slot, offset, i, filter, builder, cancellationToken);
            }

            offset += slot.FullWidth;
        }
    }

    private static void VisitSlot(
        SyntaxNode parent,
        GreenNode slot,
        int position,
        int slotIndex,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (slot is GreenToken)
        {
            ClassifyToken(new SyntaxToken(slot, parent, position), filter, builder);
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
                        VisitSlot(parent, element, elementOffset, -1, filter, builder, cancellationToken);
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
            VisitNode(child, filter, builder, cancellationToken);
        }
    }

    private static void ClassifyToken(
        SyntaxToken token,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder)
    {
        ClassifyTrivia(token.LeadingTrivia, token.Position, filter, builder);

        var span = token.Span;
        if (!token.IsMissing
            && span.Length > 0
            && Intersects(filter, span.Start, span.Length)
            && TryClassifyTokenKind(token, out var kind, out var isDeclaration))
        {
            builder.Add(new ClassifiedSpan(span, kind, isDeclaration));
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
            ClassifySkippedText(trivia.Text, position, filter, builder);
        }
    }

    /// <summary>
    /// Skipped tokens are flattened to raw text, so comments inside them (a
    /// comment on its own line is trivia on a skipped end-of-line token) are
    /// recovered by re-scanning for comment syntax.
    /// </summary>
    private static void ClassifySkippedText(
        string text,
        int position,
        TextSpan filter,
        ImmutableArray<ClassifiedSpan>.Builder builder)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                // Skip string literals so a ';' or '{' inside one is not taken
                // for a comment. Strings do not span lines; '\' escapes.
                i++;
                while (i < text.Length && text[i] != '"' && text[i] != '\n' && text[i] != '\r')
                {
                    i += text[i] == '\\' ? 2 : 1;
                }

                continue;
            }

            if (text[i] != ';' && text[i] != '{')
            {
                continue;
            }

            int end;
            if (text[i] == '{')
            {
                end = text.IndexOf('}', i + 1);
                end = end < 0 ? text.Length : end + 1;
            }
            else if (i + 1 < text.Length && text[i + 1] == '/')
            {
                end = text.IndexOf("/;", i + 2, System.StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
            }
            else
            {
                end = i;
                while (end < text.Length && text[end] != '\n' && text[end] != '\r')
                {
                    end++;
                }
            }

            if (Intersects(filter, position + i, end - i))
            {
                builder.Add(new ClassifiedSpan(new TextSpan(position + i, end - i), ClassificationKind.Comment));
            }

            i = end - 1;
        }
    }

    private static bool TryClassifyTokenKind(
        SyntaxToken token,
        out ClassificationKind kind,
        out bool isDeclaration)
    {
        isDeclaration = false;
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
                return true;
            case SyntaxKind.IdentifierToken:
                return TryClassifyIdentifier(token, out kind, out isDeclaration);
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
        out ClassificationKind kind,
        out bool isDeclaration)
    {
        kind = default;
        isDeclaration = false;
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

        switch (container)
        {
            case TypeNameSyntax or ExtendsClauseSyntax:
                kind = ClassificationKind.TypeName;
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
                return false;
        }
    }

    private static bool Intersects(TextSpan filter, int start, int width)
    {
        return start < filter.End && start + width > filter.Start;
    }
}
