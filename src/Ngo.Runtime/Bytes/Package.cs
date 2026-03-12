// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bytes
{
    [GoPackage("bytes")]
    public static class Package
    {
        [GoFunc]
        public static bool Contains(Slice<byte> b, Slice<byte> subslice)
        {
            return Index(b, subslice) >= 0;
        }

        [GoFunc]
        public static bool HasPrefix(Slice<byte> s, Slice<byte> prefix)
        {
            if (prefix.Len > s.Len) return false;
            for (int i = 0; i < prefix.Len; i++)
            {
                if (s[i] != prefix[i]) return false;
            }
            return true;
        }

        [GoFunc]
        public static bool HasSuffix(Slice<byte> s, Slice<byte> suffix)
        {
            if (suffix.Len > s.Len) return false;
            int offset = s.Len - suffix.Len;
            for (int i = 0; i < suffix.Len; i++)
            {
                if (s[offset + i] != suffix[i]) return false;
            }
            return true;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Index(Slice<byte> s, Slice<byte> sep)
        {
            if (sep.Len == 0) return 0;
            if (sep.Len > s.Len) return -1;

            for (int i = 0; i <= s.Len - sep.Len; i++)
            {
                bool match = true;
                for (int j = 0; j < sep.Len; j++)
                {
                    if (s[i + j] != sep[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        [GoFunc]
        public static bool Equal(Slice<byte> a, Slice<byte> b)
        {
            if (a.Len != b.Len) return false;
            for (int i = 0; i < a.Len; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Compare(Slice<byte> a, Slice<byte> b)
        {
            int minLen = global::System.Math.Min(a.Len, b.Len);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            if (a.Len < b.Len) return -1;
            if (a.Len > b.Len) return 1;
            return 0;
        }

        [GoFunc]
        public static Slice<byte> Repeat(Slice<byte> b, [GoParam("int")] long count)
        {
            var result = new byte[b.Len * (int)count];
            int pos = 0;
            for (long c = 0; c < count; c++)
            {
                for (int i = 0; i < b.Len; i++)
                    result[pos++] = b[i];
            }
            return new Slice<byte>(result);
        }

        [GoFunc]
        public static Slice<byte> ToUpper(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.ToUpperInvariant()));
        }

        [GoFunc]
        public static Slice<byte> ToLower(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.ToLowerInvariant()));
        }

        [GoFunc]
        public static Slice<byte> TrimSpace(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Trim()));
        }

        [GoFunc]
        public static Slice<byte> ReplaceAll(Slice<byte> s, Slice<byte> old, Slice<byte> @new)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var oldStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(old));
            var newStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(@new));
            str = str.Replace(oldStr, newStr);
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str));
        }

        [GoFunc]
        [return: GoReturn("*bytes.Buffer")]
        public static Buffer NewBuffer(Slice<byte> buf)
        {
            var b = new Buffer();
            b.Write(buf);
            return b;
        }

        [GoFunc]
        public static Slice<byte> Replace(Slice<byte> s, Slice<byte> old, Slice<byte> @new, [GoParam("int")] long n)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var oldStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(old));
            var newStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(@new));
            if (n < 0)
                str = str.Replace(oldStr, newStr);
            else
            {
                for (long i = 0; i < n; i++)
                {
                    int idx = str.IndexOf(oldStr, StringComparison.Ordinal);
                    if (idx < 0) break;
                    str = str.Substring(0, idx) + newStr + str.Substring(idx + oldStr.Length);
                }
            }
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str));
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            for (int i = 0; i < s.Len; i++)
            {
                if (f((long)s[i])) return i;
            }
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexByte(Slice<byte> s, byte c)
        {
            for (int i = 0; i < s.Len; i++)
            {
                if (s[i] == c) return i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<byte> TrimLeftFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Len && f((long)s[start]))
                start++;
            if (start == 0) return s;
            var arr = new byte[s.Len - start];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = s[start + i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        public static Slice<byte> TrimRightFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int end = s.Len;
            while (end > 0 && f((long)s[end - 1]))
                end--;
            if (end == s.Len) return s;
            var arr = new byte[end];
            for (int i = 0; i < end; i++)
                arr[i] = s[i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        public static Slice<byte> TrimPrefix(Slice<byte> s, Slice<byte> prefix)
        {
            if (!HasPrefix(s, prefix)) return s;
            var arr = new byte[s.Len - prefix.Len];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = s[prefix.Len + i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        public static Slice<byte> TrimFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Len && f((long)s[start]))
                start++;
            int end = s.Len;
            while (end > start && f((long)s[end - 1]))
                end--;
            if (start == 0 && end == s.Len) return s;
            var arr = new byte[end - start];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = s[start + i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        public static Slice<byte> TrimSuffix(Slice<byte> s, Slice<byte> suffix)
        {
            if (!HasSuffix(s, suffix)) return s;
            var arr = new byte[s.Len - suffix.Len];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = s[i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        public static bool ContainsRune(Slice<byte> s, [GoParam("rune")] long r)
        {
            var ch = (byte)r;
            for (int i = 0; i < s.Len; i++)
            {
                if (s[i] == ch) return true;
            }
            return false;
        }

        [GoFunc]
        public static Slice<byte> Trim(Slice<byte> s, string cutset)
        {
            var chars = cutset.ToCharArray();
            return TrimFunc(s, r =>
            {
                foreach (var c in chars)
                {
                    if (r == (long)c) return true;
                }
                return false;
            });
        }

        [GoFunc]
        public static Slice<Slice<byte>> Split(Slice<byte> s, Slice<byte> sep)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sepStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(sep));
            var parts = str.Split(new[] { sepStr }, StringSplitOptions.None);
            var result = new Slice<byte>[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(parts[i]));
            return new Slice<Slice<byte>>(result);
        }

        [GoFunc]
        public static bool EqualFold(Slice<byte> s, Slice<byte> t)
        {
            var sStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var tStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(t));
            return string.Equals(sStr, tStr, StringComparison.OrdinalIgnoreCase);
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Count(Slice<byte> s, Slice<byte> sep)
        {
            if (sep.Len == 0) return (long)s.Len + 1;
            long count = 0;
            for (int i = 0; i <= s.Len - sep.Len; i++)
            {
                bool match = true;
                for (int j = 0; j < sep.Len; j++)
                {
                    if (s[i + j] != sep[j]) { match = false; break; }
                }
                if (match) { count++; i += sep.Len - 1; }
            }
            return count;
        }

        [GoFunc]
        public static Slice<Slice<byte>> Fields(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var parts = str.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            var result = new Slice<byte>[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(parts[i]));
            return new Slice<Slice<byte>>(result);
        }

        [GoFunc]
        public static Slice<byte> Map([GoParam("func(rune) rune")] Func<long, long> mapping, Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sb = new StringBuilder(str.Length);
            foreach (char c in str)
                sb.Append((char)mapping(c));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        }

        [GoFunc]
        public static Slice<byte> Title(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sb = new StringBuilder(str.Length);
            bool prevSpace = true;
            foreach (char c in str)
            {
                sb.Append(prevSpace ? char.ToUpper(c) : c);
                prevSpace = char.IsWhiteSpace(c);
            }
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndex(Slice<byte> s, Slice<byte> sep)
        {
            if (sep.Len == 0) return (long)s.Len;
            for (int i = s.Len - sep.Len; i >= 0; i--)
            {
                bool match = true;
                for (int j = 0; j < sep.Len; j++)
                {
                    if (s[i + j] != sep[j]) { match = false; break; }
                }
                if (match) return (long)i;
            }
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexByte(Slice<byte> s, byte c)
        {
            for (int i = s.Len - 1; i >= 0; i--)
            {
                if (s[i] == c) return (long)i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<byte> TrimLeft(Slice<byte> s, string cutset)
        {
            int start = 0;
            while (start < s.Len && cutset.Contains((char)s[start]))
                start++;
            return s.Reslice(start, s.Len);
        }

        [GoFunc]
        public static Slice<byte> TrimRight(Slice<byte> s, string cutset)
        {
            int end = s.Len;
            while (end > 0 && cutset.Contains((char)s[end - 1]))
                end--;
            return s.Reslice(0, end);
        }

        [GoFunc]
        public static bool ContainsAny(Slice<byte> b, string chars)
        {
            for (int i = 0; i < b.Len; i++)
            {
                if (chars.Contains((char)b[i])) return true;
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexAny(Slice<byte> s, string chars)
        {
            for (int i = s.Len - 1; i >= 0; i--)
            {
                if (chars.Contains((char)s[i])) return (long)i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<byte> Join(Slice<Slice<byte>> s, Slice<byte> sep)
        {
            if (s.Len == 0) return new Slice<byte>(Array.Empty<byte>());
            if (s.Len == 1) return s[0];
            int totalLen = 0;
            for (int i = 0; i < s.Len; i++)
            {
                totalLen += s[i].Len;
                if (i > 0) totalLen += sep.Len;
            }
            var result = new byte[totalLen];
            int pos = 0;
            for (int i = 0; i < s.Len; i++)
            {
                if (i > 0)
                {
                    for (int j = 0; j < sep.Len; j++)
                        result[pos++] = sep[j];
                }
                for (int j = 0; j < s[i].Len; j++)
                    result[pos++] = s[i][j];
            }
            return new Slice<byte>(result);
        }

        [GoFunc]
        public static Slice<Slice<byte>> SplitN(Slice<byte> s, Slice<byte> sep, [GoParam("int")] long n)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sepStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(sep));
            var parts = str.Split(new[] { sepStr }, (int)n, StringSplitOptions.None);
            var result = new Slice<byte>[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(parts[i]));
            return new Slice<Slice<byte>>(result);
        }

        [GoFunc]
        public static Slice<Slice<byte>> SplitAfter(Slice<byte> s, Slice<byte> sep)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sepStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(sep));
            if (sepStr.Length == 0)
            {
                var chars = new Slice<byte>[str.Length];
                for (int i = 0; i < str.Length; i++)
                    chars[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str[i].ToString()));
                return new Slice<Slice<byte>>(chars);
            }
            var partsList = new System.Collections.Generic.List<Slice<byte>>();
            int pos = 0;
            while (true)
            {
                int idx = str.IndexOf(sepStr, pos, StringComparison.Ordinal);
                if (idx < 0) { partsList.Add(new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Substring(pos)))); break; }
                partsList.Add(new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Substring(pos, idx - pos + sepStr.Length))));
                pos = idx + sepStr.Length;
            }
            return new Slice<Slice<byte>>(partsList.ToArray());
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static Slice<byte> IndexRune(Slice<byte> s, [GoParam("rune")] long r)
        {
            return new Slice<byte>(Array.Empty<byte>());
        }

        // --- Stubs for exports in PackageRegistry but missing from runtime ---

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexAny(Slice<byte> s, string chars)
        {
            for (int i = 0; i < s.Len; i++)
            {
                if (chars.Contains((char)s[i])) return (long)i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<long> Runes(Slice<byte> s)
        {
            // stub: bytes.Runes(s []byte) []rune
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var result = new long[str.Length];
            for (int i = 0; i < str.Length; i++)
                result[i] = str[i];
            return new Slice<long>(result);
        }

        [GoFunc]
        public static Slice<byte> Clone(Slice<byte> b)
        {
            if (b.Len == 0) return new Slice<byte>(Array.Empty<byte>());
            var arr = new byte[b.Len];
            for (int i = 0; i < b.Len; i++)
                arr[i] = b[i];
            return new Slice<byte>(arr);
        }

        [GoFunc]
        [return: GoReturn("[]byte", "[]byte", "bool")]
        public static (Slice<byte>, Slice<byte>, bool) Cut(Slice<byte> s, Slice<byte> sep)
        {
            long idx = Index(s, sep);
            if (idx < 0)
                return (s, new Slice<byte>(Array.Empty<byte>()), false);
            return (s.Reslice(0, (int)idx), s.Reslice((int)idx + sep.Len, s.Len), true);
        }

        [GoFunc]
        public static Slice<Slice<byte>> FieldsFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var list = new System.Collections.Generic.List<Slice<byte>>();
            int start = -1;
            for (int i = 0; i < s.Len; i++)
            {
                if (f((long)s[i]))
                {
                    if (start >= 0)
                    {
                        var arr = new byte[i - start];
                        for (int j = 0; j < arr.Length; j++) arr[j] = s[start + j];
                        list.Add(new Slice<byte>(arr));
                        start = -1;
                    }
                }
                else if (start < 0)
                {
                    start = i;
                }
            }
            if (start >= 0)
            {
                var arr = new byte[s.Len - start];
                for (int j = 0; j < arr.Length; j++) arr[j] = s[start + j];
                list.Add(new Slice<byte>(arr));
            }
            return new Slice<Slice<byte>>(list.ToArray());
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long LastIndexFunc(Slice<byte> s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            for (int i = s.Len - 1; i >= 0; i--)
            {
                if (f((long)s[i])) return (long)i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<Slice<byte>> SplitAfterN(Slice<byte> s, Slice<byte> sep, [GoParam("int")] long n)
        {
            // stub: bytes.SplitAfterN(s, sep []byte, n int) [][]byte
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            var sepStr = global::System.Text.Encoding.UTF8.GetString(SliceToArray(sep));
            var partsList = new System.Collections.Generic.List<Slice<byte>>();
            int pos = 0;
            int count = 0;
            while (true)
            {
                if (n > 0 && count >= n - 1)
                {
                    partsList.Add(new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Substring(pos))));
                    break;
                }
                int idx = str.IndexOf(sepStr, pos, StringComparison.Ordinal);
                if (idx < 0) { partsList.Add(new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Substring(pos)))); break; }
                partsList.Add(new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.Substring(pos, idx - pos + sepStr.Length))));
                pos = idx + sepStr.Length;
                count++;
            }
            return new Slice<Slice<byte>>(partsList.ToArray());
        }

        [GoFunc]
        public static Slice<byte> ToTitle(Slice<byte> s)
        {
            var str = global::System.Text.Encoding.UTF8.GetString(SliceToArray(s));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(str.ToUpperInvariant()));
        }

        [GoFunc]
        public static Slice<byte> ToValidUTF8(Slice<byte> s, Slice<byte> replacement)
        {
            // stub: bytes.ToValidUTF8(s, replacement []byte) []byte
            // For now, return s unchanged (assumes valid UTF-8)
            return Clone(s);
        }

        [GoFunc]
        [return: GoReturn("[]byte", "bool")]
        public static (Slice<byte>, bool) CutPrefix(Slice<byte> s, Slice<byte> prefix)
        {
            if (HasPrefix(s, prefix))
                return (s.Reslice(prefix.Len, s.Len), true);
            return (s, false);
        }

        [GoFunc]
        [return: GoReturn("[]byte", "bool")]
        public static (Slice<byte>, bool) CutSuffix(Slice<byte> s, Slice<byte> suffix)
        {
            if (HasSuffix(s, suffix))
                return (s.Reslice(0, s.Len - suffix.Len), true);
            return (s, false);
        }

        [GoFunc]
        [return: GoReturn("*bytes.Buffer")]
        public static Buffer NewBufferString(string s)
        {
            var buf = new Buffer();
            buf.WriteString(s);
            return buf;
        }

        [GoFunc]
        [return: GoReturn("*bytes.Reader")]
        public static Reader NewReader(Slice<byte> b)  // Go type: bytes.Reader
        {
            return new Reader(b);
        }

        // bytes.MinRead constant
        [GoConst(Type = "int")]
        public const long MinRead = 512;

        private static byte[] SliceToArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
                arr[i] = s[i];
            return arr;
        }
    }
}
