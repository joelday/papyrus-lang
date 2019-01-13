using Antlr.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Compiler
{
    class CaseInsensitiveStringStream : ICharStream, IIntStream
    {
        private readonly ANTLRStringStream stream;

        public CaseInsensitiveStringStream(string text)
        {
            stream = new ANTLRStringStream(text + "\n");
        }

        public int CharPositionInLine
        {
            get
            {
                return stream.CharPositionInLine;
            }
            set
            {
                stream.CharPositionInLine = value;
            }
        }

        public int LT(int i)
        {
            return stream.LT(i);
        }

        public int Line
        {
            get
            {
                return stream.Line;
            }
            set
            {
                stream.Line = value;
            }
        }

        public string Substring(int start, int stop)
        {
            return stream.Substring(start, stop);
        }

        public void Consume()
        {
            stream.Consume();
        }

        public int Count => stream.Count;

#if FALLOUT4

        public int Index => stream.Index;

#elif SKYRIM

        int IIntStream.Index()
        {
            return stream.Index();
        }

#endif

        public int LA(int offset)
        {
            var number = stream.LA(offset);
            if (number == -1)
            {
                return number;
            }

            return char.ToLowerInvariant((char)number);
        }

        public int Mark()
        {
            return stream.Mark();
        }

        public void Release(int marker)
        {
            stream.Release(marker);
        }

        public void Rewind()
        {
            stream.Rewind();
        }

        public void Rewind(int marker)
        {
            stream.Rewind(marker);
        }

        public void Seek(int index)
        {
            stream.Seek(index);
        }

        public int Size()
        {
            return stream.Count;
        }

        public string SourceName => stream.SourceName;
    }
}