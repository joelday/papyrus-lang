using System;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;

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

public static class SyntaxFormatter
{
    public static ImmutableArray<IndentationEdit> GetIndentationEdits(
        SyntaxTree tree,
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
        var edits = ImmutableArray.CreateBuilder<IndentationEdit>();
        var depth = 0;
        var afterOpener = false;
        for (var line = 0; line < lines.LineCount; line++)
        {
            var start = lines.GetLineStart(line);
            var end = lines.GetLineEnd(line);
            var content = text.ToString(new TextSpan(start, end - start));
            var first = FirstNonWhitespace(content);
            if (first == content.Length)
            {
                continue;
            }

            var code = content[first..];
            var dedent = IsDedent(code);
            // A declaration's doc comment follows its header line and stays level with it.
            var headerDoc = afterOpener && code.StartsWith('{');
            var lineDepth = Math.Max(0, depth - (dedent || headerDoc ? 1 : 0));
            var expected = useTabs
                ? new string('\t', lineDepth)
                : new string(' ', lineDepth * indentSize);
            if (first != expected.Length || !content.AsSpan(0, first).SequenceEqual(expected))
            {
                edits.Add(new IndentationEdit(new TextSpan(start, first), expected));
            }

            if (dedent)
            {
                depth = lineDepth;
            }

            afterOpener = IsOpener(code);
            if (afterOpener)
            {
                depth++;
            }
        }

        return edits.ToImmutable();
    }

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

    private static int FirstNonWhitespace(string text)
    {
        var index = 0;
        while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
        {
            index++;
        }

        return index;
    }

    private static bool IsDedent(string code)
    {
        return StartsWithWord(code, "endif")
            || StartsWithWord(code, "endwhile")
            || StartsWithWord(code, "endfunction")
            || StartsWithWord(code, "endevent")
            || StartsWithWord(code, "endproperty")
            || StartsWithWord(code, "endstate")
            || StartsWithWord(code, "endstruct")
            || StartsWithWord(code, "endgroup")
            || StartsWithWord(code, "endguard")
            || StartsWithWord(code, "else")
            || StartsWithWord(code, "elseif");
    }

    private static bool IsOpener(string code)
    {
        if (StartsWithWord(code, "else") || StartsWithWord(code, "elseif"))
        {
            return true;
        }

        if (ContainsWord(code, "function") || ContainsWord(code, "event"))
        {
            return !code.Contains(" native", StringComparison.OrdinalIgnoreCase);
        }

        if (ContainsWord(code, "property"))
        {
            return !code.Contains(" auto", StringComparison.OrdinalIgnoreCase)
                && !code.Contains('=');
        }

        return StartsWithWord(code, "state")
            || StartsWithWord(code, "struct")
            || StartsWithWord(code, "group")
            || StartsWithWord(code, "if")
            || StartsWithWord(code, "while")
            || StartsWithWord(code, "guard")
            || StartsWithWord(code, "tryguard");
    }

    private static bool StartsWithWord(string text, string word)
    {
        return text.StartsWith(word, StringComparison.OrdinalIgnoreCase)
            && (text.Length == word.Length || char.IsWhiteSpace(text[word.Length]));
    }

    private static bool ContainsWord(string text, string word)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var endsAtWord =
                index + word.Length == text.Length || char.IsWhiteSpace(text[index + word.Length]);
            if ((index == 0 || char.IsWhiteSpace(text[index - 1])) && endsAtWord)
            {
                return true;
            }

            start = index + word.Length;
        }

        return false;
    }
}
