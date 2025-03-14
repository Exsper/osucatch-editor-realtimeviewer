// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.ListExtensions;
using System.Collections;

namespace osu.Framework.Lists
{
    /// <summary>
    /// A wrapper that provides a read-only view into a <see cref="List{T}"/>.
    /// This can be instantiated via the <see cref="ListExtensions.AsSlimReadOnly{T}"/> extension method.
    /// </summary>
    /// <typeparam name="T">The type of elements contained by the list.</typeparam>
    public readonly struct SlimReadOnlyListWrapper<T> : IList<T>, IList, IReadOnlyList<T>
    {
        private readonly List<T> list;

        public SlimReadOnlyListWrapper(List<T> list)
        {
            this.list = list;
        }

        public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(T item) => throw new NotSupportedException();

        public int Add(object? value) => throw new NotSupportedException();

        void IList.Clear() => throw new NotSupportedException();

        public bool Contains(object? value) => ((IList)list).Contains(value);

        public int IndexOf(object? value) => ((IList)list).IndexOf(value);

        public void Insert(int index, object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();

        public bool IsFixedSize => ((IList)list).IsFixedSize;

        bool IList.IsReadOnly => true;

        object? IList.this[int index]
        {
            get => ((IList)list)[index];
            set => throw new NotSupportedException();
        }

        void ICollection<T>.Clear() => throw new NotSupportedException();

        public bool Contains(T item) => list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

        public bool Remove(T item) => throw new NotSupportedException();

        public void CopyTo(Array array, int index) => ((ICollection)list).CopyTo(array, index);

        public int Count => list.Count;

        public bool IsSynchronized => ((ICollection)list).IsSynchronized;

        public object SyncRoot => ((ICollection)list).SyncRoot;

        int ICollection<T>.Count => list.Count;

        bool ICollection<T>.IsReadOnly => true;

        public int IndexOf(T item) => list.IndexOf(item);

        public void Insert(int index, T item) => throw new NotSupportedException();

        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        public T this[int index]
        {
            get => list[index];
            set => throw new NotSupportedException();
        }
    }
}
