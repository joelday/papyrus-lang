using System;
using System.Collections;
using System.Collections.Generic;

namespace Papyrus.CodeAnalysis.Syntax;

internal readonly struct SyntaxList<T> : IEnumerable<T> where T : SyntaxNode
{
    internal SyntaxList(GreenNode? node, SyntaxNode? parent = null, int position = 0)
    {
        Node = node;
        Parent = parent;
        Position = position;
    }

    internal GreenNode? Node { get; }

    internal SyntaxNode? Parent { get; }

    internal int Position { get; }

    public int Count
    {
        get
        {
            if (Node is null)
            {
                return 0;
            }

            return Node.Kind == SyntaxKind.List ? Node.SlotCount : 1;
        }
    }

    public T Get(int index)
    {
        if (Node is null || (uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (Node.Kind != SyntaxKind.List)
        {
            return (T)Node.CreateRed(Parent, Position);
        }

        var offset = Position;
        for (var i = 0; i < index; i++)
        {
            offset += Node.GetSlot(i)?.FullWidth ?? 0;
        }

        return (T)Node.GetSlot(index)!.CreateRed(Parent, offset);
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private readonly SyntaxList<T> _list;
        private int _index;

        internal Enumerator(SyntaxList<T> list)
        {
            _list = list;
            _index = -1;
        }

        public T Current => _list.Get(_index);

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= _list.Count)
            {
                return false;
            }

            _index = next;
            return true;
        }

        public void Reset() => _index = -1;

        public void Dispose()
        {
        }
    }
}
