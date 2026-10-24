using System;
using System.Buffers;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Foreach-only, pooled equivalent of List.ToArray(). Each enumeration owns a separate
    // buffer, including recursive blasts. Mutating the source does not change its order or
    // membership; newly spawned fragments still wait for the next simulation tick.
    public static class NativeSnapshots
    {
        public static Snapshot<T> Capture<T>(List<T> source) => new Snapshot<T>(source);

        public readonly struct Snapshot<T>
        {
            private readonly List<T> source;
            internal Snapshot(List<T> source) { this.source = source; }
            public Enumerator<T> GetEnumerator() => new Enumerator<T>(source);
        }

        public struct Enumerator<T> : IDisposable
        {
            private T[] items;
            private readonly int count;
            private int index;
            internal Enumerator(List<T> source)
            {
                count = source.Count;
                index = -1;
                items = count == 0 ? null : ArrayPool<T>.Shared.Rent(count);
                if (items != null) source.CopyTo(items);
            }
            public T Current => items[index];
            public bool MoveNext() => ++index < count;
            public void Dispose()
            {
                if (items == null) return;
                // Do not retain destroyed bodies, pilots or an old arena in the shared pool.
                ArrayPool<T>.Shared.Return(items, clearArray: true);
                items = null;
            }
        }
    }
}
