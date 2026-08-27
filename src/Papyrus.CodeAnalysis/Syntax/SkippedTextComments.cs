using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Syntax;

internal enum SkippedCommentKind
{
    Line,
    Block,
    Doc,
}

internal readonly struct SkippedComment
{
    internal SkippedComment(TextSpan span, SkippedCommentKind kind)
    {
        Span = span;
        Kind = kind;
    }

    internal TextSpan Span { get; }

    internal SkippedCommentKind Kind { get; }
}

/// <summary>
/// Skipped tokens are flattened to raw text
/// (<c>Parser.BuildSkippedTrivia</c>), so comments inside them — a comment on
/// its own line is trivia on a skipped end-of-line token — are recovered by
/// re-scanning the text for comment syntax. String literals are skipped so
/// their contents cannot open a comment.
/// </summary>
internal static class SkippedTextComments
{
    internal static ImmutableArray<SkippedComment> Collect(string text, int position)
    {
        var builder = ImmutableArray<SkippedComment>.Empty.ToBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                // Strings do not span lines; '\' escapes.
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
            SkippedCommentKind kind;
            if (text[i] == '{')
            {
                kind = SkippedCommentKind.Doc;
                end = text.IndexOf('}', i + 1);
                end = end < 0 ? text.Length : end + 1;
            }
            else if (i + 1 < text.Length && text[i + 1] == '/')
            {
                kind = SkippedCommentKind.Block;
                end = text.IndexOf("/;", i + 2, System.StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
            }
            else
            {
                kind = SkippedCommentKind.Line;
                end = i;
                while (end < text.Length && text[end] != '\n' && text[end] != '\r')
                {
                    end++;
                }
            }

            builder.Add(new SkippedComment(new TextSpan(position + i, end - i), kind));
            i = end - 1;
        }

        return builder.ToImmutable();
    }
}
