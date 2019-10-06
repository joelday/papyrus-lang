using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    // Converted from https://github.com/Microsoft/vscode-languageserver-node/blob/master/types/src/main.ts

    public interface IReadOnlyScriptText
    {
        string FilePath { get; }
        string Text { get; }
        string Version { get; }

        string GetTextInRange(TextRange range);

        int OffsetAt(TextPosition position);
        TextPosition PositionAt(int offset);
    }

    public class ScriptText : IReadOnlyScriptText, IEquatable<ScriptText>
    {
        private List<int> _lineOffsets;

        public string FilePath { get; }
        public string Text { get; private set; }
        public string Version { get; private set; }

        public ScriptText(string filePath, string text, string version)
        {
            FilePath = filePath;
            Text = text;
            Version = version;
        }

        private List<int> GetLineOffsets()
        {
            if (_lineOffsets == null)
            {
                var lineOffsets = new List<int>();

                var isLineStart = true;
                for (var i = 0; i < Text.Length; i++)
                {
                    if (isLineStart)
                    {
                        lineOffsets.Add(i);
                        isLineStart = false;
                    }
                    var ch = Text[i];
                    isLineStart = (ch == '\r' || ch == '\n');
                    if (ch == '\r' && i + 1 < Text.Length && Text[(i + 1)] == '\n')
                    {
                        i++;
                    }
                }
                if (isLineStart && Text.Length > 0)
                {
                    lineOffsets.Add(Text.Length);
                }

                _lineOffsets = lineOffsets;
            }

            return _lineOffsets;
        }

        public string GetTextInRange(TextRange range)
        {
            var start = OffsetAt(range.Start);
            var end = OffsetAt(range.End);

            if (end <= start)
            {
                return string.Empty;
            }

            return Text.Substring(start, end - start);
        }

        public void Update(string version, IEnumerable<ScriptTextChange> changes)
        {
            Version = version;

            foreach (var change in changes)
            {
                var startOffset = OffsetAt(change.Range.Start);
                var newPrepend = Text.Substring(0, startOffset);

                var endOffset = startOffset + change.RangeLength;
                var newAppend = Text.Substring(endOffset);

                Text = newPrepend + change.Text + newAppend;

                _lineOffsets = null;
            }
        }

        public int OffsetAt(TextPosition position)
        {
            var lineOffsets = GetLineOffsets();

            if (position.Line >= lineOffsets.Count)
            {
                return Text.Length;
            }
            else if (position.Line < 0)
            {
                return 0;
            }

            var lineOffset = lineOffsets[(int)position.Line];
            var nextLineOffset = (position.Line + 1 < lineOffsets.Count) ? lineOffsets[(int)position.Line + 1] : Text.Length;

            return (int)Math.Max(Math.Min(lineOffset + position.Character, nextLineOffset), lineOffset);
        }

        public TextPosition PositionAt(int offset)
        {
            offset = Math.Max(Math.Min(offset, Text.Length), 0);

            var lineOffsets = GetLineOffsets();
            var low = 0;
            var high = lineOffsets.Count;

            if (high == 0)
            {
                return new TextPosition(0, offset);
            }
            while (low < high)
            {
                var mid = (int)Math.Floor((double)((low + high) / 2));

                if (lineOffsets[mid] > offset)
                {
                    high = mid;
                }
                else
                {
                    low = mid + 1;
                }
            }
            // low is the least x for which the line offset is larger than the current offset
            // or array.length if no line offset is larger than the current offset
            var line = low - 1;
            return new TextPosition(line, offset - lineOffsets[line]);
        }

        public bool Equals(ScriptText other)
        {
            return other != null && (FilePath == other.FilePath && Version == other.Version);
        }
    }
}