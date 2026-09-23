using System;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using GreenToken = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenToken;
using GreenTrivia = Papyrus.CodeAnalysis.Syntax.InternalSyntax.GreenTrivia;

namespace Papyrus.CodeAnalysis.Structure;

public readonly struct IndentationEdit
{
    public IndentationEdit(TextSpan span, string newText)
    {
        Span = span;
        NewText = newText;
    }

    public TextSpan Span { get; }

    public string NewText { get; }
}

/// <summary>
/// Indentation from the syntax tree: a line is indented one level per block (function,
/// event, full property, state, struct, group, guard, If, While) that encloses it. A
/// block only counts once its End keyword is present, so a native function or auto
/// property never indents what follows, and keywords in comments or strings mean
/// nothing. Doc comments stay level with the header they document; the inner lines of
/// multi-line comments and line-continued statements keep the author's alignment.
/// </summary>
public static class SyntaxFormatter
{
    public static ImmutableArray<IndentationEdit> GetIndentationEdits(
        SyntaxTree tree,
        int indentSize = 4,
        bool useTabs = false
    ) => GetIndentationEdits(tree, span: null, indentSize, useTabs);

    /// <summary>The edits for lines that intersect <paramref name="span"/> (all lines when null).</summary>
    public static ImmutableArray<IndentationEdit> GetIndentationEdits(
        SyntaxTree tree,
        TextSpan? span,
        int indentSize = 4,
        bool useTabs = false
    )
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (indentSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize));
        }

        var text = tree.GetText();
        var lines = text.Lines;
        var depths = ComputeLineDepths(tree);
        var firstLine = span is { } s ? lines.GetPosition(s.Start).Line : 0;
        var lastLine = span is { } e ? lines.GetPosition(Math.Max(e.Start, e.End - 1)).Line : lines.LineCount - 1;
        var edits = ImmutableArray.CreateBuilder<IndentationEdit>();
        for (var line = firstLine; line <= lastLine && line < lines.LineCount; line++)
        {
            if (depths[line] is not { } depth)
            {
                continue;
            }

            var start = lines.GetLineStart(line);
            var content = text.ToString(new TextSpan(start, lines.GetLineEnd(line) - start));
            var first = FirstNonWhitespace(content);
            if (first == content.Length)
            {
                continue;
            }

            var expected = useTabs ? new string('\t', depth) : new string(' ', depth * indentSize);
            if (first != expected.Length || !content.AsSpan(0, first).SequenceEqual(expected))
            {
                edits.Add(new IndentationEdit(new TextSpan(start, first), expected));
            }
        }

        return edits.ToImmutable();
    }

    /// <summary>
    /// Whether <paramref name="line"/> starts with a keyword that closes or continues a
    /// block (EndIf, Else, EndFunction, ...): the lines worth re-indenting as they are typed.
    /// </summary>
    public static bool StartsWithBlockKeyword(SyntaxTree tree, int line)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var text = tree.GetText();
        var lines = text.Lines;
        if (line < 0 || line >= lines.LineCount)
        {
            return false;
        }

        var start = lines.GetLineStart(line);
        var code = text.ToString(new TextSpan(start, lines.GetLineEnd(line) - start)).TrimStart();
        foreach (var keyword in BlockKeywords)
        {
            if (code.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                && (code.Length == keyword.Length || !char.IsLetterOrDigit(code[keyword.Length]) && code[keyword.Length] != '_'))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] BlockKeywords =
    [
        "EndIf", "EndWhile", "EndFunction", "EndEvent", "EndProperty", "EndState", "EndStruct",
        "EndGroup", "EndGuard", "EndLock", "ElseIf", "Else",
    ];

    public static string ApplyIndentation(SyntaxTree tree, int indentSize = 4, bool useTabs = false)
    {
        var text = tree.GetText().ToString();
        var edits = GetIndentationEdits(tree, indentSize, useTabs);
        for (var i = edits.Length - 1; i >= 0; i--)
        {
            var edit = edits[i];
            text = text.Remove(edit.Span.Start, edit.Span.Length)
                .Insert(edit.Span.Start, edit.NewText);
        }

        return text;
    }

    /// <summary>Each line's indentation depth, or null for lines to leave alone.</summary>
    private static int?[] ComputeLineDepths(SyntaxTree tree)
    {
        var walker = new DepthWalker(tree.GetText());
        walker.Visit(tree.GetRoot().Green, 0, parentIsBlock: false);
        return walker.Depths;
    }

    private sealed class DepthWalker
    {
        private readonly string _text;
        private readonly TextLineMap _lines;
        private readonly bool[] _keep;
        private int _depth;
        private int _pendingOpens;

        public DepthWalker(SourceText text)
        {
            _text = text.ToString();
            _lines = text.Lines;
            Depths = new int?[_lines.LineCount];
            _keep = new bool[_lines.LineCount];
        }

        public int?[] Depths { get; }

        public void Visit(GreenNode node, int position, bool parentIsBlock)
        {
            // Lists are transparent: their elements belong to the enclosing node.
            var isList = node.Kind == SyntaxKind.List;
            var isBlock = isList ? parentIsBlock : IsBlock(node);
            if (isBlock && !isList)
            {
                // Opens after the block's first token (its header line).
                _pendingOpens++;
            }

            for (var i = 0; i < node.SlotCount; i++)
            {
                if (node.GetSlot(i) is not { } slot)
                {
                    continue;
                }

                if (slot is GreenToken token)
                {
                    VisitToken(token, position, isBlock);
                }
                else
                {
                    Visit(slot, position, isBlock);
                }

                position += slot.FullWidth;
            }
        }

        private void VisitToken(GreenToken token, int position, bool parentIsBlock)
        {
            position = VisitTrivia(token.Leading, position);
            if (token.IsMissing || token.Text.Length == 0 || token.Kind == SyntaxKind.EndOfLineToken)
            {
                VisitTrivia(token.Trailing, position + token.Text.Length);
                return;
            }

            var closes = IsCloser(token.Kind) && parentIsBlock || token.Kind is SyntaxKind.ElseKeyword or SyntaxKind.ElseIfKeyword;
            if (closes)
            {
                _depth = Math.Max(0, _depth - 1);
            }

            // A doc comment inside a block documents its header line.
            var depth = token.Kind == SyntaxKind.DocCommentToken && parentIsBlock ? Math.Max(0, _depth - 1) : _depth;
            Record(position, depth);
            LeaveInnerLines(position, token.Text);

            if (token.Kind is SyntaxKind.ElseKeyword or SyntaxKind.ElseIfKeyword)
            {
                _depth++;
            }

            _depth += _pendingOpens;
            _pendingOpens = 0;
            VisitTrivia(token.Trailing, position + token.Text.Length);
        }

        private int VisitTrivia(GreenNode? trivia, int position)
        {
            if (trivia is null)
            {
                return position;
            }

            if (trivia is GreenTrivia single)
            {
                VisitSingleTrivia(single, position);
                return position + single.FullWidth;
            }

            for (var i = 0; i < trivia.SlotCount; i++)
            {
                if (trivia.GetSlot(i) is GreenTrivia item)
                {
                    VisitSingleTrivia(item, position);
                    position += item.FullWidth;
                }
            }

            return position;
        }

        private void VisitSingleTrivia(GreenTrivia trivia, int position)
        {
            switch (trivia.Kind)
            {
                case SyntaxKind.LineCommentTrivia:
                case SyntaxKind.BlockCommentTrivia:
                    Record(position, _depth);
                    LeaveInnerLines(position, trivia.Text);
                    break;
                case SyntaxKind.SkippedTokensTrivia:
                    // Blank lines (and whatever error recovery skipped) are kept as skipped
                    // text; comments on those lines are indented like any other.
                    foreach (var comment in SkippedTextComments.Collect(trivia.Text, position))
                    {
                        Record(comment.Span.Start, _depth);
                        LeaveInnerLines(comment.Span.Start, _text.Substring(comment.Span.Start, comment.Span.Length));
                    }

                    break;
                case SyntaxKind.LineContinuationTrivia:
                    // The continued line keeps its alignment.
                    var next = _lines.GetPosition(position).Line + 1;
                    if (next < _keep.Length)
                    {
                        _keep[next] = true;
                    }

                    break;
            }
        }

        // Only the first thing on a line decides its indentation.
        private void Record(int position, int depth)
        {
            var line = _lines.GetPosition(position).Line;
            if (Depths[line] is not null || _keep[line])
            {
                return;
            }

            var lineStart = _lines.GetLineStart(line);
            for (var i = lineStart; i < position; i++)
            {
                if (_text[i] is not (' ' or '\t'))
                {
                    _keep[line] = true;
                    return;
                }
            }

            Depths[line] = depth;
        }

        private void LeaveInnerLines(int position, string text)
        {
            var first = _lines.GetPosition(position).Line;
            var last = _lines.GetPosition(position + Math.Max(0, text.Length - 1)).Line;
            for (var line = first + 1; line <= last && line < _keep.Length; line++)
            {
                _keep[line] = true;
            }
        }

        private static bool IsBlock(GreenNode node)
        {
            for (var i = 0; i < node.SlotCount; i++)
            {
                if (node.GetSlot(i) is GreenToken { IsMissing: false } token && IsCloser(token.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCloser(SyntaxKind kind) =>
            kind is SyntaxKind.EndIfKeyword
                or SyntaxKind.EndWhileKeyword
                or SyntaxKind.EndFunctionKeyword
                or SyntaxKind.EndEventKeyword
                or SyntaxKind.EndPropertyKeyword
                or SyntaxKind.EndStateKeyword
                or SyntaxKind.EndStructKeyword
                or SyntaxKind.EndGroupKeyword
                or SyntaxKind.EndGuardKeyword
                or SyntaxKind.EndLockKeyword;
    }

    private static int FirstNonWhitespace(string text)
    {
        var index = 0;
        while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
        {
            index++;
        }

        return index;
    }
}
