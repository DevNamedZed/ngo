// -----------------------------------------------------------------------
// <copyright file="Slice.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go slice: a view over a backing array with (array, offset, length, capacity).
    /// Value type — cheap to copy, shares the underlying array.
    /// </summary>
    public readonly struct Slice<T> : IEnumerable<T>
    {
        private readonly T[]? _array;
        private readonly int _offset;

        /// <summary>Creates a slice backed by the entire array.</summary>
        public Slice(T[] array)
        {
            _array = array ?? throw new ArgumentNullException(nameof(array));
            _offset = 0;
            Len = array.Length;
            Cap = array.Length;
        }

        /// <summary>Creates a slice backed by a region of the array.</summary>
        public Slice(T[] array, int offset, int length)
            : this(array, offset, length, array.Length - offset)
        {
        }

        /// <summary>Creates a slice with explicit offset, length, and capacity.</summary>
        public Slice(T[] array, int offset, int length, int capacity)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (offset < 0 || offset > array.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || offset + length > array.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (capacity < length || offset + capacity > array.Length)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _array = array;
            _offset = offset;
            Len = length;
            Cap = capacity;
        }

        /// <summary>Creates a slice using make([]T, len, cap).</summary>
        public static Slice<T> Make(int length, int capacity = -1)
        {
            if (capacity < 0) capacity = length;
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (capacity < length) throw new ArgumentOutOfRangeException(nameof(capacity));

            var array = new T[capacity];
            return new Slice<T>(array, 0, length, capacity);
        }

        /// <summary>Number of elements in the slice.</summary>
        public int Len { get; }

        /// <summary>Capacity: number of elements from slice start to end of backing array region.</summary>
        public int Cap { get; }

        /// <summary>True if the slice is nil (zero value).</summary>
        public bool IsNil => _array == null;

        /// <summary>Returns a Span over the slice's elements.</summary>
        public Span<T> AsSpan()
        {
            if (_array == null) return Span<T>.Empty;
            return _array.AsSpan(_offset, Len);
        }

        /// <summary>Returns a ReadOnlySpan over the slice's elements.</summary>
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            if (_array == null) return ReadOnlySpan<T>.Empty;
            return new ReadOnlySpan<T>(_array, _offset, Len);
        }

        /// <summary>Element access by index. Panics on out-of-range.</summary>
        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Len)
                    throw new GoPanicException($"runtime error: index out of range [{index}] with length {Len}");
                return ref _array![_offset + index];
            }
        }

        /// <summary>Swaps elements at indices i and j.</summary>
        public void Swap(int i, int j)
        {
            if ((uint)i >= (uint)Len)
                throw new GoPanicException($"runtime error: index out of range [{i}] with length {Len}");
            if ((uint)j >= (uint)Len)
                throw new GoPanicException($"runtime error: index out of range [{j}] with length {Len}");
            T tmp = _array![_offset + i];
            _array[_offset + i] = _array[_offset + j];
            _array[_offset + j] = tmp;
        }

        /// <summary>2-index reslice: s[low:high]</summary>
        public Slice<T> Reslice(int low, int high)
        {
            if (high == -1) high = Len; // sentinel: omitted high bound defaults to len
            if (low < 0 || high < low || high > Cap)
                throw new GoPanicException($"runtime error: slice bounds out of range [{low}:{high}] with capacity {Cap}");
            return new Slice<T>(_array!, _offset + low, high - low, Cap - low);
        }

        /// <summary>3-index reslice: s[low:high:max]</summary>
        public Slice<T> Reslice(int low, int high, int max)
        {
            if (low < 0 || high < low || max < high || max > Cap)
                throw new GoPanicException($"runtime error: slice bounds out of range [{low}:{high}:{max}] with capacity {Cap}");
            return new Slice<T>(_array!, _offset + low, high - low, max - low);
        }

        /// <summary>Append elements, growing the backing array if needed.</summary>
        public static Slice<T> Append(Slice<T> s, params T[] elems)
        {
            var newLen = s.Len + elems.Length;

            if (s.IsNil)
            {
                // Appending to nil slice
                var arr = new T[GrowCapacity(0, newLen)];
                Array.Copy(elems, 0, arr, 0, elems.Length);
                return new Slice<T>(arr, 0, newLen, arr.Length);
            }

            if (newLen <= s.Cap)
            {
                // Fits in existing capacity — copy into backing array
                Array.Copy(elems, 0, s._array!, s._offset + s.Len, elems.Length);
                return new Slice<T>(s._array!, s._offset, newLen, s.Cap);
            }

            // Need to grow — allocate new array
            var newCap = GrowCapacity(s.Cap, newLen);
            var newArray = new T[newCap];
            Array.Copy(s._array!, s._offset, newArray, 0, s.Len);
            Array.Copy(elems, 0, newArray, s.Len, elems.Length);
            return new Slice<T>(newArray, 0, newLen, newCap);
        }

        /// <summary>Append one slice to another.</summary>
        public static Slice<T> Append(Slice<T> s, Slice<T> other)
        {
            if (other.IsNil || other.Len == 0) return s;

            var newLen = s.Len + other.Len;

            if (s.IsNil)
            {
                var arr = new T[GrowCapacity(0, newLen)];
                other.AsReadOnlySpan().CopyTo(arr.AsSpan());
                return new Slice<T>(arr, 0, newLen, arr.Length);
            }

            if (newLen <= s.Cap)
            {
                other.AsReadOnlySpan().CopyTo(s._array.AsSpan(s._offset + s.Len));
                return new Slice<T>(s._array!, s._offset, newLen, s.Cap);
            }

            var newCap = GrowCapacity(s.Cap, newLen);
            var newArray = new T[newCap];
            s.AsReadOnlySpan().CopyTo(newArray.AsSpan());
            other.AsReadOnlySpan().CopyTo(newArray.AsSpan(s.Len));
            return new Slice<T>(newArray, 0, newLen, newCap);
        }

        /// <summary>Copy from src to dst, returning the number of elements copied.</summary>
        public static int Copy(Slice<T> dst, Slice<T> src)
        {
            if (dst.IsNil || src.IsNil) return 0;
            var n = global::System.Math.Min(dst.Len, src.Len);
            if (n == 0) return 0;
            Array.Copy(src._array!, src._offset, dst._array!, dst._offset, n);
            return n;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Len; i++)
            {
                yield return _array![_offset + i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Growth strategy matching Go's slice growth.</summary>
        private static int GrowCapacity(int oldCap, int needed)
        {
            var newCap = oldCap;
            if (newCap == 0) newCap = 1;

            while (newCap < needed)
            {
                if (oldCap < 256)
                {
                    newCap = newCap * 2;
                }
                else
                {
                    newCap = newCap + newCap / 4;
                }
            }

            return newCap;
        }
    }
}
