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
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Unicode;

namespace Ngo.Runtime
{
    /// <summary>
    /// String helpers bridging Go's UTF-8 string semantics and .NET's UTF-16 strings.
    /// Go: strings are immutable byte sequences (UTF-8), len() returns byte count,
    /// s[i] returns the i-th byte, for-range iterates runes.
    /// .NET: strings are UTF-16 char sequences.
    /// </summary>
    public static class GoString
    {
        private const int StackAllocThreshold = 256;

        /// <summary>Go len(s) — returns UTF-8 byte count.</summary>
        public static int Len(string s)
        {
            if (s == null) return 0;
            if (System.Text.Ascii.IsValid(s)) return s.Length;
            return global::System.Text.Encoding.UTF8.GetByteCount(s);
        }

        /// <summary>Go s[i] — returns the i-th UTF-8 byte.</summary>
        public static byte ByteAt(string s, int index)
        {
            if (System.Text.Ascii.IsValid(s))
            {
                if ((uint)index >= (uint)s.Length)
                    throw new GoPanicException($"runtime error: index out of range [{index}] with length {s.Length}");
                return (byte)s[index];
            }

            int byteOffset = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                int runeByteLen = rune.Utf8SequenceLength;
                if (index < byteOffset + runeByteLen)
                {
                    Span<byte> buf = stackalloc byte[4];
                    rune.EncodeToUtf8(buf);
                    return buf[index - byteOffset];
                }
                byteOffset += runeByteLen;
            }

            throw new GoPanicException($"runtime error: index out of range [{index}] with length {byteOffset}");
        }

        /// <summary>Convert string to []byte (UTF-8 encoding).</summary>
        public static Slice<byte> ToBytes(string s)
        {
            if (s == null) return default;

            if (System.Text.Ascii.IsValid(s))
            {
                var bytes = new byte[s.Length];
                System.Text.Ascii.FromUtf16(s, bytes, out _);
                return new Slice<byte>(bytes);
            }

            var byteCount = global::System.Text.Encoding.UTF8.GetByteCount(s);
            var arr = new byte[byteCount];
            global::System.Text.Encoding.UTF8.TryGetBytes(s.AsSpan(), arr, out _);
            return new Slice<byte>(arr);
        }

        /// <summary>Convert []byte to string (UTF-8 decoding).</summary>
        public static string FromBytes(Slice<byte> bytes)
        {
            if (bytes.IsNil) return "";
            return global::System.Text.Encoding.UTF8.GetString(bytes.AsReadOnlySpan());
        }

        /// <summary>Convert string to []rune (Unicode code points).</summary>
        public static Slice<int> ToRunes(string s)
        {
            if (s == null) return default;

            int count = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                count++;
            }

            var arr = new int[count];
            int idx = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                arr[idx++] = rune.Value;
            }

            return new Slice<int>(arr);
        }

        /// <summary>Convert []rune to string.</summary>
        public static string FromRunes(Slice<int> runes)
        {
            if (runes.IsNil) return "";
            var sb = new StringBuilder(runes.Len);
            var span = runes.AsReadOnlySpan();
            for (int i = 0; i < span.Length; i++)
            {
                sb.Append(char.ConvertFromUtf32(span[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Iterate runes in a string (for i, r := range s).
        /// Yields (byteIndex, rune) pairs matching Go's range-over-string semantics.
        /// </summary>
        public static IEnumerable<(int index, int rune)> RangeRunes(string s)
        {
            if (s == null) yield break;

            if (System.Text.Ascii.IsValid(s))
            {
                for (int i = 0; i < s.Length; i++)
                {
                    yield return (i, s[i]);
                }
                yield break;
            }

            int byteIndex = 0;
            foreach (var rune in s.EnumerateRunes())
            {
                yield return (byteIndex, rune.Value);
                byteIndex += rune.Utf8SequenceLength;
            }
        }

        /// <summary>Go string(runeValue) — convert a single rune to a string.</summary>
        public static string FromRune(int rune)
        {
            return char.ConvertFromUtf32(rune);
        }

        /// <summary>Go string slicing s[low:high] — operates on byte indices.</summary>
        public static string SliceString(string s, int low, int high)
        {
            if (System.Text.Ascii.IsValid(s))
            {
                if (low < 0 || high < low || high > s.Length)
                    throw new GoPanicException($"runtime error: slice bounds out of range [{low}:{high}] with length {s.Length}");
                return s.Substring(low, high - low);
            }

            var byteCount = global::System.Text.Encoding.UTF8.GetByteCount(s);
            if (low < 0 || high < low || high > byteCount)
                throw new GoPanicException($"runtime error: slice bounds out of range [{low}:{high}] with length {byteCount}");

            byte[]? rented = null;
            try
            {
                Span<byte> buffer = byteCount <= StackAllocThreshold
                    ? stackalloc byte[byteCount]
                    : (rented = ArrayPool<byte>.Shared.Rent(byteCount)).AsSpan(0, byteCount);

                global::System.Text.Encoding.UTF8.GetBytes(s.AsSpan(), buffer);
                return global::System.Text.Encoding.UTF8.GetString(buffer.Slice(low, high - low));
            }
            finally
            {
                if (rented != null) ArrayPool<byte>.Shared.Return(rented);
            }
        }

    }
}
