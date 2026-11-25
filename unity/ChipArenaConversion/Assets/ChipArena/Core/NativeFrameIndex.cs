using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // For detached, immutable frame arrays only. Reuse the index between render frames,
    // including when two different arrays describe the same actors in a different order.
    internal sealed class NativeFrameIndex<TKey, TRow> where TRow : class
    {
        private readonly Func<TRow, TKey> key;
        private readonly Dictionary<TKey, TRow> rows = new Dictionary<TKey, TRow>();
        private TRow[] source;

        public NativeFrameIndex(Func<TRow, TKey> key) { this.key = key; }

        public void Bind(TRow[] next)
        {
            if (ReferenceEquals(source, next)) return;
            rows.Clear();
            if (next != null)
            {
                // Reverse insertion keeps the first match, as the original linear search did.
                for (int i = next.Length - 1; i >= 0; i--)
                    rows[key(next[i])] = next[i];
            }
            source = next;
        }

        public TRow Find(TKey id) => rows.TryGetValue(id, out var row) ? row : null;
    }
}
