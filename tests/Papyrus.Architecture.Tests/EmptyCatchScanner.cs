using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Papyrus.Architecture.Tests;

/// <summary>
/// Source scan for empty / default-returning catch blocks. Prefer this over a
/// dedicated analyzer project: the two production libraries are still empty and
/// the rule is "never swallow."
/// </summary>
internal static class EmptyCatchScanner
{
    public static IReadOnlyList<string> ScanProject(string projectDirectory)
    {
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedOrBuildOutput(file))
            {
                continue;
            }

            ScanFile(file, projectDirectory, violations);
        }

        return violations;
    }

    internal static IReadOnlyList<string> ScanText(string source, string relativeFileName = "snippet.cs")
    {
        var violations = new List<string>();
        ScanContent(source, relativeFileName, violations);
        return violations;
    }

    private static bool IsGeneratedOrBuildOutput(string file)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var parts = file.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || part.Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ScanFile(string file, string projectDirectory, List<string> violations)
    {
        var source = File.ReadAllText(file);
        var relative = Path.GetRelativePath(projectDirectory, file);
        ScanContent(source, relative, violations);
    }

    private static void ScanContent(string source, string relative, List<string> violations)
    {
        var masked = MaskNonCode(source);

        for (var i = 0; i < masked.Length; i++)
        {
            if (!IsCatchKeyword(masked, i))
            {
                continue;
            }

            var afterCatch = i + 5;
            SkipWhitespace(masked, ref afterCatch);

            // catch (Type) [when (filter)]  or  catch when (filter)
            if (afterCatch < masked.Length && masked[afterCatch] == '(')
            {
                afterCatch = SkipBalanced(masked, afterCatch, '(', ')');
                if (afterCatch < 0)
                {
                    continue;
                }

                SkipWhitespace(masked, ref afterCatch);
            }

            if (IsKeywordAt(masked, afterCatch, "when"))
            {
                afterCatch += 4;
                SkipWhitespace(masked, ref afterCatch);
                if (afterCatch < masked.Length && masked[afterCatch] == '(')
                {
                    afterCatch = SkipBalanced(masked, afterCatch, '(', ')');
                    if (afterCatch < 0)
                    {
                        continue;
                    }
                }
            }

            SkipWhitespace(masked, ref afterCatch);
            if (afterCatch >= masked.Length || masked[afterCatch] != '{')
            {
                continue;
            }

            var bodyEnd = SkipBalanced(masked, afterCatch, '{', '}');
            if (bodyEnd < 0)
            {
                continue;
            }

            var body = masked.Substring(afterCatch + 1, bodyEnd - afterCatch - 2);
            if (!IsSwallowingBody(body))
            {
                continue;
            }

            var line = LineNumber(source, i);
            violations.Add(relative + ":" + line + ": empty or default-returning catch");
            i = bodyEnd - 1;
        }
    }

    internal static bool IsSwallowingBody(string body)
    {
        var text = CompactWhitespace(body);
        var i = 0;
        SkipWhitespace(text, ref i);
        if (i >= text.Length)
        {
            return true;
        }

        if (!MatchKeyword(text, ref i, "return"))
        {
            return false;
        }

        SkipWhitespace(text, ref i);
        if (IsEndOfReturn(text, i))
        {
            return true;
        }

        if (MatchKeyword(text, ref i, "null"))
        {
            SkipWhitespace(text, ref i);
            if (i < text.Length && text[i] == '!')
            {
                i++;
                SkipWhitespace(text, ref i);
            }

            return IsEndOfReturn(text, i);
        }

        // default keyword only — not identifiers like defaultValue / defaultBinding.
        if (!MatchKeyword(text, ref i, "default"))
        {
            return false;
        }

        SkipWhitespace(text, ref i);
        if (i < text.Length && text[i] == '(')
        {
            var close = SkipBalanced(text, i, '(', ')');
            if (close < 0)
            {
                return false;
            }

            i = close;
            SkipWhitespace(text, ref i);
        }

        if (i < text.Length && text[i] == '!')
        {
            i++;
            SkipWhitespace(text, ref i);
        }

        return IsEndOfReturn(text, i);
    }

    private static bool IsEndOfReturn(string text, int semicolonIndex)
        => semicolonIndex < text.Length
           && text[semicolonIndex] == ';'
           && semicolonIndex == text.Length - 1;

    private static bool MatchKeyword(string text, ref int index, string keyword)
    {
        if (!IsKeywordAt(text, index, keyword))
        {
            return false;
        }

        index += keyword.Length;
        return true;
    }

    private static string CompactWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace && ch != ';' && ch != '(' && ch != ')' && sb[^1] != '(')
            {
                sb.Append(' ');
            }

            pendingSpace = false;
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static bool IsCatchKeyword(string text, int index)
        => IsKeywordAt(text, index, "catch");

    private static bool IsKeywordAt(string text, int index, string keyword)
    {
        if (index + keyword.Length > text.Length)
        {
            return false;
        }

        if (!text.AsSpan(index, keyword.Length).Equals(keyword, StringComparison.Ordinal))
        {
            return false;
        }

        if (index > 0 && IsIdentifierPart(text[index - 1]))
        {
            return false;
        }

        if (index + keyword.Length < text.Length && IsIdentifierPart(text[index + keyword.Length]))
        {
            return false;
        }

        return true;
    }

    private static bool IsIdentifierPart(char ch)
        => char.IsLetterOrDigit(ch) || ch == '_';

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static int SkipBalanced(string text, int openIndex, char open, char close)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }

        return -1;
    }

    private static int LineNumber(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Replaces comments and string/char literals with spaces so keyword search
    /// ignores documentation. Newlines are kept so line numbers stay aligned.
    /// </summary>
    internal static string MaskNonCode(string source)
    {
        var sb = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch == '/' && i + 1 < source.Length)
            {
                if (source[i + 1] == '/')
                {
                    i = MaskUntil(source, sb, i, newline: true);
                    continue;
                }

                if (source[i + 1] == '*')
                {
                    i = MaskBlockComment(source, sb, i);
                    continue;
                }
            }

            if (ch is '"' or '@' or '$' || (ch == '\'' && LooksLikeCharLiteral(source, i)))
            {
                i = MaskLiteral(source, sb, i);
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static bool LooksLikeCharLiteral(string source, int index)
        => index + 1 < source.Length && source[index] == '\'';

    private static int MaskUntil(string source, StringBuilder sb, int start, bool newline)
    {
        var i = start;
        while (i < source.Length)
        {
            var ch = source[i];
            if (newline && (ch == '\n' || ch == '\r'))
            {
                break;
            }

            sb.Append(ch is '\n' or '\r' ? ch : ' ');
            i++;
        }

        return i - 1;
    }

    private static int MaskBlockComment(string source, StringBuilder sb, int start)
    {
        var i = start;
        while (i < source.Length)
        {
            var ch = source[i];
            if (ch == '*' && i + 1 < source.Length && source[i + 1] == '/')
            {
                sb.Append("  ");
                return i + 1;
            }

            sb.Append(ch is '\n' or '\r' ? ch : ' ');
            i++;
        }

        return source.Length - 1;
    }

    private static int MaskLiteral(string source, StringBuilder sb, int start)
    {
        var i = start;
        var isVerbatim = false;
        var isRaw = false;
        var rawQuoteCount = 0;

        while (i < source.Length && source[i] is '@' or '$')
        {
            if (source[i] == '@')
            {
                isVerbatim = true;
            }

            sb.Append(' ');
            i++;
        }

        if (i < source.Length && source[i] == '\'')
        {
            sb.Append(' ');
            i++;
            while (i < source.Length)
            {
                var ch = source[i];
                if (ch == '\\' && i + 1 < source.Length)
                {
                    sb.Append("  ");
                    i += 2;
                    continue;
                }

                sb.Append(ch is '\n' or '\r' ? ch : ' ');
                if (ch == '\'')
                {
                    return i;
                }

                i++;
            }

            return source.Length - 1;
        }

        if (i < source.Length && source[i] == '"')
        {
            var quotes = 0;
            while (i + quotes < source.Length && source[i + quotes] == '"')
            {
                quotes++;
            }

            if (quotes >= 3)
            {
                isRaw = true;
                rawQuoteCount = quotes;
            }
        }

        if (isRaw)
        {
            for (var q = 0; q < rawQuoteCount; q++)
            {
                sb.Append(' ');
            }

            i += rawQuoteCount;
            while (i < source.Length)
            {
                if (IsRawClose(source, i, rawQuoteCount))
                {
                    for (var q = 0; q < rawQuoteCount; q++)
                    {
                        sb.Append(' ');
                    }

                    return i + rawQuoteCount - 1;
                }

                sb.Append(source[i] is '\n' or '\r' ? source[i] : ' ');
                i++;
            }

            return source.Length - 1;
        }

        if (i >= source.Length || source[i] != '"')
        {
            sb.Append(source[start] is '\n' or '\r' ? source[start] : ' ');
            return start;
        }

        sb.Append(' ');
        i++;
        while (i < source.Length)
        {
            var ch = source[i];
            if (isVerbatim)
            {
                if (ch == '"' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    continue;
                }

                sb.Append(ch is '\n' or '\r' ? ch : ' ');
                if (ch == '"')
                {
                    return i;
                }

                i++;
                continue;
            }

            if (ch == '\\' && i + 1 < source.Length)
            {
                sb.Append("  ");
                i += 2;
                continue;
            }

            sb.Append(ch is '\n' or '\r' ? ch : ' ');
            if (ch == '"')
            {
                return i;
            }

            i++;
        }

        return source.Length - 1;
    }

    private static bool IsRawClose(string source, int index, int quoteCount)
    {
        if (index + quoteCount > source.Length)
        {
            return false;
        }

        for (var q = 0; q < quoteCount; q++)
        {
            if (source[index + q] != '"')
            {
                return false;
            }
        }

        return true;
    }
}
