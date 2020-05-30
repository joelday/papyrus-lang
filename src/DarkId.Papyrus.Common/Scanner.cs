using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class Scanner<T>
    {
        private readonly Stack<T> _left = new Stack<T>();
        private readonly Stack<T> _right = new Stack<T>();
        private readonly int _startingOffset;

        private readonly IEnumerator<T> _enumerator;

        private bool _enumeratorDone;
        private T _current;

        public Scanner(IEnumerable<T> enumerable, int startingOffset = 0)
        {
            _enumerator = enumerable.GetEnumerator();
            _startingOffset = startingOffset;
        }

        public T Current => _current;
        public bool Done => _enumeratorDone && _right.Count == 0;
        public int CurrentOffset => _startingOffset + _left.Count;

        public T Peek(int count = 1)
        {
            if (Next())
            {
                var current = count > 1 ? Peek(count - 1) : Current;

                if (current != null)
                {
                    Previous();
                }

                return current;
            }

            return default(T);
        }

        public bool Next()
        {
            if (Done)
            {
                return false;
            }

            if (_right.Count > 0)
            {
                _left.Push(_current);
                _current = _right.Pop();

                return true;
            }

            var moved = _enumerator.MoveNext();
            if (moved)
            {
                _left.Push(_current);
                _current = _enumerator.Current;
            }
            else
            {
                _enumeratorDone = true;
                return false;
            }

            return true;
        }

        public bool Previous()
        {
            if (_left.Count == 0)
            {
                return false;
            }

            _right.Push(_current);
            _current = _left.Pop();

            return true;
        }
    }
}
