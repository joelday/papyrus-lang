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

        private readonly IEnumerator<T> _enumerator;

        private bool _enumeratorDone;
        private T _current;

        public Scanner(IEnumerable<T> enumerable)
        {
            _enumerator = enumerable.GetEnumerator();
        }

        public T Current => _current;
        public bool Done => _enumeratorDone && _right.Count == 0;
        public bool PeekDone => Peek() == null;

        public T Peek()
        {
            if (Next())
            {
                var current = Current;
                Previous();
                return current;
            }

            return default(T);
        }

        public T PeekPrevious()
        {
            if (Previous())
            {
                var current = Current;
                Next();
                return current;
            }

            return default(T);
        }

        public IEnumerable<T> AllRemaining()
        {
            while (Next())
            {
                yield return Current;
            }
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

        public U DoLookahead<U>(Func<U> lookaheadFunc)
        {
            var currentLeftStackLength = _left.Count;

            try
            {
                return lookaheadFunc();
            }
            finally
            {
                while (_left.Count != currentLeftStackLength)
                {
                    if (_left.Count > currentLeftStackLength)
                    {
                        Previous();
                    }
                    else
                    {
                        Next();
                    }
                }
            }
        }

        public void DoLookahead(Action lookaheadFunc)
        {
            DoLookahead(() =>
            {
                lookaheadFunc();
                return 0;
            });
        }
    }
}
