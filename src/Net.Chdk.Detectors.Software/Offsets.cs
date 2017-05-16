using System.Collections;
using System.Collections.Generic;

namespace Net.Chdk.Detectors.Software
{
    sealed class Offsets : IEnumerable<int>
    {
        #region Node

        private sealed class Node
        {
            public int Value { get; }
            public Node Previous { get; }

            public Node(int value, Node previous)
            {
                Value = value;
                Previous = previous;
            }
        }

        #endregion

        #region Enumerator

        private sealed class Enumerator : IEnumerator<int>
        {
            private Offsets offsets;
            private Node node;

            public Enumerator(Offsets offsets)
            {
                this.offsets = offsets;
            }

            public void Dispose()
            {
                offsets = null;
                node = null;
            }

            public void Reset()
            {
                node = null;
            }

            public int Current => node.Value;

            public bool MoveNext()
            {
                node = node != null
                    ? node.Previous
                    : offsets.Last;
                return node != null;
            }

            object IEnumerator.Current => Current;
        }

        #endregion

        public static Offsets Empty = new Offsets();

        private Node Last { get; }

        private Offsets()
        {
        }

        public Offsets(Offsets prefix, int last)
        {
            var previous = prefix.Last != null
                ? new Node(prefix.Last.Value, prefix.Last.Previous)
                : null;
            Last = new Node(last, previous);
        }

        public IEnumerator<int> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
