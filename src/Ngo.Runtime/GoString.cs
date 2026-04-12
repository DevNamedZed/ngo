// -----------------------------------------------------------------------
// <copyright file="GoString.cs" company="Ziad">
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
using System.Collections.Generic;
using System.Text;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go string value type. Backed by a UTF-8 byte array with offset and length,
    /// matching Go's string semantics: immutable byte sequence, len() returns byte count,
    /// s[i] returns the i-th byte, for-range iterates runes. Slicing is O(1) and shares
    /// the underlying storage.
    /// </summary>
    public readonly struct GoString : IEquatable<GoString>, IComparable<GoString>
    {
        private readonly byte[]? _bytes;
        private readonly int _offset;
        private readonly int _length;

        public GoString(byte[] bytes, int offset, int length)
        {
            _bytes = bytes;
            _offset = offset;
            _length = length;
        }

        public GoString(byte[] bytes)
        {
            _bytes = bytes;
            _offset = 0;
            _length = bytes.Length;
        }

        /// <summary>Go len(s) — returns UTF-8 byte count.</summary>
        public int Len => _length;

        /// <summary>Go s[i] — returns the i-th byte.</summary>
        public byte this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_length)
                {
                    throw new GoPanicException(
                        $"runtime error: index out of range [{index}] with length {_length}");
                }
                return (_bytes ?? Array.Empty<byte>())[_offset + index];
            }
        }

        /// <summary>Go s[low:high] — O(1) slice sharing the backing array.</summary>
        public GoString Slice(int low, int high)
        {
            if (high < 0)
            {
                high = _length;
            }
            if (low < 0 || high < low || high > _length)
            {
                throw new GoPanicException(
                    $"runtime error: slice bounds out of range [{low}:{high}] with length {_length}");
            }
            if (_bytes == null)
            {
                return default;
            }
            return new GoString(_bytes, _offset + low, high - low);
        }

        /// <summary>Create a GoString from a .NET string literal (UTF-16 → UTF-8).</summary>
        public static GoString FromNetString(string value)
        {
            if (value == null || value.Length == 0)
            {
                return default;
            }
            var bytes = Encoding.UTF8.GetBytes(value);
            return new GoString(bytes, 0, bytes.Length);
        }

        /// <summary>Convert to .NET string (UTF-8 → UTF-16) for interop.</summary>
        public string ToNetString()
        {
            if (_bytes == null || _length == 0)
            {
                return "";
            }
            return Encoding.UTF8.GetString(_bytes, _offset, _length);
        }

        /// <summary>Get a ReadOnlySpan over the underlying UTF-8 bytes.</summary>
        public ReadOnlySpan<byte> AsSpan()
        {
            if (_bytes == null)
            {
                return ReadOnlySpan<byte>.Empty;
            }
            return new ReadOnlySpan<byte>(_bytes, _offset, _length);
        }

        /// <summary>Convert Go string to []byte (copy, since Go copies on conversion).</summary>
        public static Slice<byte> ToBytes(GoString source)
        {
            if (source._bytes == null || source._length == 0)
            {
                return new Slice<byte>(Array.Empty<byte>());
            }
            var copy = new byte[source._length];
            Array.Copy(source._bytes, source._offset, copy, 0, source._length);
            return new Slice<byte>(copy);
        }

        /// <summary>Convert []byte to Go string (copy).</summary>
        public static GoString FromBytes(Slice<byte> bytes)
        {
            if (bytes.IsNil || bytes.Len == 0)
            {
                return default;
            }
            var copy = new byte[bytes.Len];
            for (int i = 0; i < bytes.Len; i++)
            {
                copy[i] = bytes[i];
            }
            return new GoString(copy, 0, copy.Length);
        }

        /// <summary>Convert Go string to []rune (Unicode code points).</summary>
        public static Slice<int> ToRunes(GoString source)
        {
            if (source._bytes == null || source._length == 0)
            {
                return default;
            }
            var span = source.AsSpan();
            var runes = new List<int>();
            int position = 0;
            while (position < span.Length)
            {
                var status = System.Text.Rune.DecodeFromUtf8(span.Slice(position), out var rune, out int bytesConsumed);
                if (status != System.Buffers.OperationStatus.Done)
                {
                    runes.Add(0xFFFD);
                    position++;
                }
                else
                {
                    runes.Add(rune.Value);
                    position += bytesConsumed;
                }
            }
            return new Slice<int>(runes.ToArray());
        }

        /// <summary>Convert []rune to Go string.</summary>
        public static GoString FromRunes(Slice<int> runes)
        {
            if (runes.IsNil || runes.Len == 0)
            {
                return default;
            }
            var sb = new StringBuilder();
            var span = runes.AsReadOnlySpan();
            for (int i = 0; i < span.Length; i++)
            {
                sb.Append(char.ConvertFromUtf32(span[i]));
            }
            return FromNetString(sb.ToString());
        }

        /// <summary>Go string(runeValue) — convert a single rune to a string.</summary>
        public static GoString FromRune(int rune)
        {
            return FromNetString(char.ConvertFromUtf32(rune));
        }

        /// <summary>
        /// Iterate runes in a string (for i, r := range s).
        /// Yields (byteIndex, rune) pairs matching Go's range-over-string semantics.
        /// </summary>
        public static IEnumerable<(int index, int rune)> RangeRunes(GoString source)
        {
            if (source._bytes == null || source._length == 0)
            {
                yield break;
            }
            var bytes = source._bytes;
            int offset = source._offset;
            int end = offset + source._length;
            int position = 0;
            while (offset < end)
            {
                byte leading = bytes[offset];
                if (leading < 0x80)
                {
                    yield return (position, leading);
                    offset++;
                    position++;
                }
                else
                {
                    var remaining = new ReadOnlySpan<byte>(bytes, offset, end - offset);
                    var status = System.Text.Rune.DecodeFromUtf8(remaining, out var rune, out int bytesConsumed);
                    if (status != System.Buffers.OperationStatus.Done)
                    {
                        yield return (position, 0xFFFD);
                        offset++;
                        position++;
                    }
                    else
                    {
                        yield return (position, rune.Value);
                        offset += bytesConsumed;
                        position += bytesConsumed;
                    }
                }
            }
        }

        // --- Operators ---

        public static GoString operator +(GoString left, GoString right)
        {
            if (left._length == 0)
            {
                return right;
            }
            if (right._length == 0)
            {
                return left;
            }
            var combined = new byte[left._length + right._length];
            Array.Copy(left._bytes!, left._offset, combined, 0, left._length);
            Array.Copy(right._bytes!, right._offset, combined, left._length, right._length);
            return new GoString(combined, 0, combined.Length);
        }

        public static bool operator ==(GoString left, GoString right) => left.Equals(right);
        public static bool operator !=(GoString left, GoString right) => !left.Equals(right);
        public static bool operator <(GoString left, GoString right) => left.CompareTo(right) < 0;
        public static bool operator >(GoString left, GoString right) => left.CompareTo(right) > 0;
        public static bool operator <=(GoString left, GoString right) => left.CompareTo(right) <= 0;
        public static bool operator >=(GoString left, GoString right) => left.CompareTo(right) >= 0;

        // --- Implicit conversion from .NET string for ease of use in runtime code ---

        public static implicit operator GoString(string value) => FromNetString(value);

        // --- IEquatable / IComparable ---

        public bool Equals(GoString other)
        {
            if (_length != other._length)
            {
                return false;
            }
            return AsSpan().SequenceEqual(other.AsSpan());
        }

        public int CompareTo(GoString other)
        {
            return AsSpan().SequenceCompareTo(other.AsSpan());
        }

        public override bool Equals(object? obj)
        {
            return obj is GoString other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (_bytes == null || _length == 0)
            {
                return 0;
            }
            var hash = new HashCode();
            var span = AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                hash.Add(span[i]);
            }
            return hash.ToHashCode();
        }

        public override string ToString() => ToNetString();
    }
}
