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

        #region Static Members

        private const int OffsetsLength = 8;

        public static Offsets Empty = new Offsets();

        public static int GetOffsetCount(int maxLength)
        {
            if (maxLength == 1)
                return 1;
            return maxLength * GetOffsetCount(maxLength - 1);
        }

        #endregion

        #region Instance Members

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

        public void GetAllOffsets(uint?[] offsets, ref int index, int pos, int maxLength)
        {
            if (pos == maxLength)
                offsets[index++] = GetOffsets();
            else
                GetOffsets(offsets, ref index, pos, maxLength);
        }

        private void GetOffsets(uint?[] offsets, ref int index, int pos, int max)
        {
            for (var i = 0; i < OffsetsLength; i++)
            {
                if (!Contains(i))
                {
                    var prefix2 = new Offsets(this, i);
                    prefix2.GetAllOffsets(offsets, ref index, pos + 1, max);
                }
            }
        }

        private bool Contains(int i)
        {
            for (var node = Last; node != null; node = node.Previous)
                if (node.Value == i)
                    return true;
            return false;
        }

        private uint? GetOffsets()
        {
            var uOffsets = 0u;
            var index = 0;
            for (var node = Last; node != null; node = node.Previous)
            {
                uOffsets += (uint)node.Value << (index << 2);
                index++;
            }
            return uOffsets;
        }

        #endregion
    }
}
