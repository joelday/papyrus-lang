using System;
using System.Collections.Generic;

namespace Papyrus.CodeAnalysis.Text;

/// <summary>
/// Maps between character offsets and line/character positions. Characters
/// are UTF-16 code units, matching both <see cref="SourceText"/> indexing and
/// LSP's default position encoding. Line breaks are <c>\r\n</c>, <c>\n</c>,
/// or <c>\r</c>.
/// </summary>
public sealed class TextLineMap
{
    private readonly int[] _lineStarts;
    private readonly int[] _lineContentEnds;
    private readonly int _length;

    private TextLineMap(int[] lineStarts, int[] lineContentEnds, int length)
    {
        _lineStarts = lineStarts;
        _lineContentEnds = lineContentEnds;
        _length = length;
    }

    internal static TextLineMap Create(SourceText text)
    {
        var starts = new List<int> { 0 };
        var contentEnds = new List<int>();
        var length = text.Length;
        for (var i = 0; i < length; i++)
        {
            var c = text.GetChar(i);
            if (c is '\r' or '\n')
            {
                contentEnds.Add(i);
                if (c == '\r' && i + 1 < length && text.GetChar(i + 1) == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        contentEnds.Add(length);
        return new TextLineMap([.. starts], [.. contentEnds], length);
    }

    public int LineCount => _lineStarts.Length;

    public int TextLength => _length;

    public (int Line, int Character) GetPosition(int position)
    {
        position = Math.Clamp(position, 0, _length);
        var index = Array.BinarySearch(_lineStarts, position);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return (index, position - _lineStarts[index]);
    }

    public int GetLineStart(int line)
    {
        return line < _lineStarts.Length ? _lineStarts[line] : _length;
    }

    /// <summary>End of the line's content, excluding the line break.</summary>
    public int GetLineEnd(int line)
    {
        return line < _lineContentEnds.Length ? _lineContentEnds[line] : _length;
    }

    public int GetOffset(int line, int character)
    {
        if (line < 0)
        {
            return 0;
        }

        if (line >= _lineStarts.Length)
        {
            return _length;
        }

        var start = _lineStarts[line];
        var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] : _length;
        return (int)Math.Min(start + Math.Max((long)character, 0), end);
    }
}
