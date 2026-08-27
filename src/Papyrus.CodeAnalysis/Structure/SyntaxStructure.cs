using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using GreenToken = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken;
using GreenTrivia = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenTrivia;

namespace Papyrus.CodeAnalysis.Structure;

public enum FoldingSpanKind
{
    Block,
    Comment,
    Imports,
}

public readonly struct FoldingSpan
{
    public FoldingSpan(TextSpan span, FoldingSpanKind kind)
    {
        Span = span;
        Kind = kind;
    }

    public TextSpan Span { get; }

    public FoldingSpanKind Kind { get; }

    public override string ToString() => $"{Span} {Kind}";
}

/// <summary>
/// Structural facts about a syntax tree that need no symbol information:
/// foldable regions and the ancestor span chain used for selection ranges.
/// Single-line folding candidates are returned as-is; clients that fold by
/// line are expected to drop spans that do not cross a line break.
/// </summary>
public static class SyntaxStructure
{
    public static ImmutableArray<FoldingSpan> GetFoldingSpans(
        SyntaxTree tree,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(tree);
        var builder = ImmutableArray.CreateBuilder<FoldingSpan>();
        VisitFolding(tree.GetRoot(), builder, cancellationToken);
        builder.Sort(static (a, b) => a.Span.Start != b.Span.Start
            ? a.Span.Start.CompareTo(b.Span.Start)
            : b.Span.End.CompareTo(a.Span.End));
        return builder.ToImmutable();
    }

    /// <summary>
    /// Spans of every node and token containing <paramref name="position"/>,
    /// innermost first. Empty when the position is outside the tree.
    /// </summary>
    public static ImmutableArray<TextSpan> GetSelectionSpans(
        SyntaxTree tree,
        int position,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(tree);
        var root = tree.GetRoot();
        if (position < root.FullSpan.Start || position >= root.FullSpan.End)
        {
            return ImmutableArray<TextSpan>.Empty;
        }

        var outerFirst = ImmutableArray.CreateBuilder<TextSpan>();
        CollectSelectionSpans(root, position, outerFirst, cancellationToken);

        var result = ImmutableArray.CreateBuilder<TextSpan>(outerFirst.Count);
        for (var i = outerFirst.Count - 1; i >= 0; i--)
        {
            // Drop spans equal to the next-outer span so each step widens.
            if (result.Count == 0 || result[^1] != outerFirst[i])
            {
                result.Add(outerFirst[i]);
            }
        }

        return result.ToImmutable();
    }

    private static void VisitFolding(
        SyntaxNode node,
        ImmutableArray<FoldingSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddNodeFolding(node, builder);

        var green = node.Green;
        var offset = node.Position;
        for (var i = 0; i < green.SlotCount; i++)
        {
            var slot = green.GetSlot(i);
            if (slot is null)
            {
                continue;
            }

            VisitFoldingSlot(node, slot, offset, i, builder, cancellationToken);
            offset += slot.FullWidth;
        }
    }

    private static void VisitFoldingSlot(
        SyntaxNode parent,
        GreenNode slot,
        int position,
        int slotIndex,
        ImmutableArray<FoldingSpan>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (slot is GreenToken token)
        {
            AddTokenFolding(token, position, builder);
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
                    VisitFoldingSlot(parent, element, elementOffset, -1, builder, cancellationToken);
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
            VisitFolding(child, builder, cancellationToken);
        }
    }

    private static void AddNodeFolding(SyntaxNode node, ImmutableArray<FoldingSpan>.Builder builder)
    {
        switch (node)
        {
            case ScriptDeclarationSyntax script:
                AddImportsFolding(script, builder);
                break;
            case FunctionDeclarationSyntax function:
                AddBlock(builder, HeaderStart(function.ReturnType?.Span.Start, function.FunctionKeyword), node);
                break;
            case EventDeclarationSyntax @event:
                AddBlock(builder, HeaderStart(null, @event.EventKeyword), node);
                break;
            case PropertyDeclarationSyntax property when property.Accessors.Count > 0:
                AddBlock(builder, HeaderStart(property.TypeName.Span.Start, property.PropertyKeyword), node);
                break;
            case StateDeclarationSyntax state:
                AddBlock(
                    builder,
                    HeaderStart(state.AutoKeyword.IsMissing || state.AutoKeyword.Kind == SyntaxKind.None
                        ? null
                        : state.AutoKeyword.Span.Start, state.StateKeyword),
                    node);
                break;
            case StructDeclarationSyntax @struct:
                AddBlock(builder, HeaderStart(null, @struct.StructKeyword), node);
                break;
            case GroupDeclarationSyntax group:
                AddBlock(builder, HeaderStart(null, group.GroupKeyword), node);
                break;
            case IfStatementSyntax @if:
                AddBlock(builder, HeaderStart(null, @if.IfKeyword), node);
                break;
            case ElseIfClauseSyntax elseIf:
                AddBlock(builder, HeaderStart(null, elseIf.ElseIfKeyword), node);
                break;
            case ElseClauseSyntax @else:
                AddBlock(builder, HeaderStart(null, @else.ElseKeyword), node);
                break;
            case WhileStatementSyntax @while:
                AddBlock(builder, HeaderStart(null, @while.WhileKeyword), node);
                break;
            case GuardStatementSyntax guard:
                AddBlock(builder, HeaderStart(null, guard.GuardKeyword), node);
                break;
            case TryGuardStatementSyntax tryGuard:
                AddBlock(builder, HeaderStart(null, tryGuard.TryGuardKeyword), node);
                break;
        }
    }

    private static void AddImportsFolding(
        ScriptDeclarationSyntax script,
        ImmutableArray<FoldingSpan>.Builder builder)
    {
        var imports = script.Imports;
        if (imports.Count < 2)
        {
            return;
        }

        var start = imports.Get(0).Span.Start;
        var end = imports.Get(imports.Count - 1).Span.End;
        if (end > start)
        {
            builder.Add(new FoldingSpan(new TextSpan(start, end - start), FoldingSpanKind.Imports));
        }
    }

    private static int? HeaderStart(int? preferred, SyntaxToken keyword)
    {
        if (preferred is { } value)
        {
            return value;
        }

        return keyword.IsMissing || keyword.Kind == SyntaxKind.None ? null : keyword.Span.Start;
    }

    private static void AddBlock(
        ImmutableArray<FoldingSpan>.Builder builder,
        int? start,
        SyntaxNode node)
    {
        var span = node.Span;
        var from = start ?? span.Start;
        if (span.End > from)
        {
            builder.Add(new FoldingSpan(new TextSpan(from, span.End - from), FoldingSpanKind.Block));
        }
    }

    private static void AddTokenFolding(
        GreenToken token,
        int position,
        ImmutableArray<FoldingSpan>.Builder builder)
    {
        var offset = position;
        AddTriviaFolding(token.Leading, ref offset, builder);
        if (token.Kind == SyntaxKind.DocCommentToken && token.Text.Length > 0)
        {
            builder.Add(new FoldingSpan(new TextSpan(offset, token.Text.Length), FoldingSpanKind.Comment));
        }

        offset += token.Text.Length;
        AddTriviaFolding(token.Trailing, ref offset, builder);
    }

    private static void AddTriviaFolding(
        GreenNode? trivia,
        ref int position,
        ImmutableArray<FoldingSpan>.Builder builder)
    {
        if (trivia is null)
        {
            return;
        }

        if (trivia is GreenTrivia single)
        {
            AddSingleTriviaFolding(single, position, builder);
            position += single.FullWidth;
            return;
        }

        for (var i = 0; i < trivia.SlotCount; i++)
        {
            if (trivia.GetSlot(i) is GreenTrivia item)
            {
                AddSingleTriviaFolding(item, position, builder);
                position += item.FullWidth;
            }
        }
    }

    private static void AddSingleTriviaFolding(
        GreenTrivia trivia,
        int position,
        ImmutableArray<FoldingSpan>.Builder builder)
    {
        if (trivia.Kind == SyntaxKind.BlockCommentTrivia && trivia.FullWidth > 0)
        {
            builder.Add(new FoldingSpan(new TextSpan(position, trivia.FullWidth), FoldingSpanKind.Comment));
        }
        else if (trivia.Kind == SyntaxKind.SkippedTokensTrivia)
        {
            foreach (var comment in SkippedTextComments.Collect(trivia.Text, position))
            {
                if (comment.Kind is SkippedCommentKind.Block or SkippedCommentKind.Doc)
                {
                    builder.Add(new FoldingSpan(comment.Span, FoldingSpanKind.Comment));
                }
            }
        }
    }

    private static void CollectSelectionSpans(
        SyntaxNode node,
        int position,
        ImmutableArray<TextSpan>.Builder outerFirst,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var span = node.Span;
        if (span.Length > 0 && position >= span.Start && position < span.End)
        {
            outerFirst.Add(span);
        }

        var green = node.Green;
        var offset = node.Position;
        for (var i = 0; i < green.SlotCount; i++)
        {
            var slot = green.GetSlot(i);
            if (slot is null)
            {
                continue;
            }

            if (position >= offset && position < offset + slot.FullWidth)
            {
                CollectSelectionSlot(node, slot, offset, i, position, outerFirst, cancellationToken);
                return;
            }

            offset += slot.FullWidth;
        }
    }

    private static void CollectSelectionSlot(
        SyntaxNode parent,
        GreenNode slot,
        int position,
        int slotIndex,
        int target,
        ImmutableArray<TextSpan>.Builder outerFirst,
        CancellationToken cancellationToken)
    {
        if (slot is GreenToken)
        {
            var token = new SyntaxToken(slot, parent, position);
            var span = token.Span;
            if (span.Length > 0 && target >= span.Start && target < span.End)
            {
                outerFirst.Add(span);
            }

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
                    if (target >= elementOffset && target < elementOffset + element.FullWidth)
                    {
                        CollectSelectionSlot(parent, element, elementOffset, -1, target, outerFirst, cancellationToken);
                        return;
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
            CollectSelectionSpans(child, target, outerFirst, cancellationToken);
        }
    }
}
