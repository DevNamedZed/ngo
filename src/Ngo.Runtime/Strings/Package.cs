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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Strings
{
    [GoPackage("strings")]
    public static class Package
    {
        [GoFunc]
        public static bool Contains(string s, string substr) => s.Contains(substr);

        [GoFunc]
        public static bool HasPrefix(string s, string prefix) => s.StartsWith(prefix);

        [GoFunc]
        public static bool HasSuffix(string s, string suffix) => s.EndsWith(suffix);

        [GoFunc]
        public static string Join(Slice<string> elems, string sep)
        {
            var parts = new string[elems.Len];
            for (int i = 0; i < elems.Len; i++)
            {
                parts[i] = elems[i];
            }
            return string.Join(sep, parts);
        }

        [GoFunc]
        public static Slice<string> Split(string s, string sep)
        {
            var parts = s.Split(new[] { sep }, StringSplitOptions.None);
            return new Slice<string>(parts);
        }

        [GoFunc]
        public static string Replace(string s, string old, string @new, long n)
        {
            if (n < 0) return s.Replace(old, @new);

            var result = s;
            for (long i = 0; i < n; i++)
            {
                int idx = result.IndexOf(old, StringComparison.Ordinal);
                if (idx < 0) break;
                result = result.Substring(0, idx) + @new + result.Substring(idx + old.Length);
            }
            return result;
        }

        [GoFunc]
        public static string TrimSpace(string s) => s.Trim();

        [GoFunc]
        public static string ToUpper(string s) => s.ToUpper();

        [GoFunc]
        public static string ToLower(string s) => s.ToLower();

        [GoFunc]
        public static long Index(string s, string substr) => s.IndexOf(substr, StringComparison.Ordinal);

        [GoFunc]
        public static string Repeat(string s, long count)
        {
            if (count <= 0) return "";
            var sb = new System.Text.StringBuilder(s.Length * (int)count);
            for (long i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }

        [GoFunc]
        public static string ReplaceAll(string s, string old, string @new) => s.Replace(old, @new);

        [GoFunc]
        public static string Trim(string s, string cutset) => s.Trim(cutset.ToCharArray());

        [GoFunc]
        public static string TrimPrefix(string s, string prefix) =>
            s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;

        [GoFunc]
        public static string TrimSuffix(string s, string suffix) =>
            s.EndsWith(suffix) ? s.Substring(0, s.Length - suffix.Length) : s;

        [GoFunc]
        public static string TrimLeft(string s, string cutset) => s.TrimStart(cutset.ToCharArray());

        [GoFunc]
        public static string TrimRight(string s, string cutset) => s.TrimEnd(cutset.ToCharArray());

        [GoFunc]
        public static long Count(string s, string substr)
        {
            if (string.IsNullOrEmpty(substr)) return (long)s.Length + 1;
            long count = 0;
            int idx = 0;
            while ((idx = s.IndexOf(substr, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += substr.Length;
            }
            return count;
        }

        [GoFunc]
        public static bool EqualFold(string s, string t) =>
            string.Equals(s, t, StringComparison.OrdinalIgnoreCase);

        [GoFunc]
        public static Slice<string> Fields(string s)
        {
            var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return new Slice<string>(parts);
        }

        [GoFunc]
        public static long LastIndex(string s, string substr) =>
            (long)s.LastIndexOf(substr, StringComparison.Ordinal);

        [GoFunc]
        public static bool ContainsRune(string s, [GoParam("rune")] long r) =>
            s.IndexOf((char)r) >= 0;

        [GoFunc]
        public static bool ContainsAny(string s, string chars)
        {
            foreach (char c in chars)
            {
                if (s.IndexOf(c) >= 0) return true;
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("string", "string", "bool")]
        public static (string before, string after, bool found) Cut(string s, string sep)
        {
            int idx = s.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0)
            {
                return (s, "", false);
            }
            return (s.Substring(0, idx), s.Substring(idx + sep.Length), true);
        }

        [GoFunc]
        public static string Map([GoParam("func(rune) rune")] Func<long, long> mapping, string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                var r = mapping((long)c);
                if (r >= 0)
                    sb.Append((char)r);
            }

            return sb.ToString();
        }

        [GoFunc]
        public static Slice<string> SplitN(string s, string sep, long n)
        {
            if (n == 0) return new Slice<string>(Array.Empty<string>());
            var parts = s.Split(new[] { sep }, (int)n, StringSplitOptions.None);
            return new Slice<string>(parts);
        }

        [GoFunc]
        public static Slice<string> SplitAfter(string s, string sep)
        {
            if (string.IsNullOrEmpty(sep))
                return Split(s, sep);
            var result = new System.Collections.Generic.List<string>();
            int start = 0;
            while (true)
            {
                int idx = s.IndexOf(sep, start, StringComparison.Ordinal);
                if (idx < 0)
                {
                    result.Add(s.Substring(start));
                    break;
                }

                result.Add(s.Substring(start, idx - start + sep.Length));
                start = idx + sep.Length;
            }

            return new Slice<string>(result.ToArray());
        }

        [GoFunc]
        public static Slice<string> SplitAfterN(string s, string sep, long n)
        {
            if (n == 0) return new Slice<string>(Array.Empty<string>());
            if (n == 1) return new Slice<string>(new[] { s });
            var all = SplitAfter(s, sep);
            if (n < 0 || all.Len <= (int)n)
                return all;
            var result = new string[(int)n];
            for (int i = 0; i < (int)n - 1; i++)
                result[i] = all[i];
            var rest = new System.Text.StringBuilder();
            for (int i = (int)n - 1; i < all.Len; i++)
                rest.Append(all[i]);
            result[(int)n - 1] = rest.ToString();
            return new Slice<string>(result);
        }

        [GoFunc]
        public static string Title(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool prev = true; // treat start as after space
            foreach (var c in s)
            {
                sb.Append(prev ? char.ToUpper(c) : c);
                prev = char.IsWhiteSpace(c);
            }

            return sb.ToString();
        }

        [GoFunc]
        public static long IndexByte(string s, byte c)
        {
            return s.IndexOf((char)c);
        }

        [GoFunc]
        public static long IndexRune(string s, [GoParam("rune")] long r)
        {
            return s.IndexOf((char)r);
        }

        [GoFunc]
        public static long IndexAny(string s, string chars)
        {
            return s.IndexOfAny(chars.ToCharArray());
        }

        [GoFunc]
        public static string TrimFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Length && f((long)s[start]))
                start++;
            int end = s.Length;
            while (end > start && f((long)s[end - 1]))
                end--;
            return s.Substring(start, end - start);
        }

        [GoFunc]
        public static string TrimRightFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int end = s.Length;
            while (end > 0 && f((long)s[end - 1]))
                end--;
            return s.Substring(0, end);
        }

        [GoFunc]
        public static string TrimLeftFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Length && f((long)s[start]))
                start++;
            return s.Substring(start);
        }

        [GoFunc]
        public static long IndexFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (f((long)s[i])) return i;
            }
            return -1;
        }

        [GoFunc]
        public static long LastIndexFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (f((long)s[i])) return i;
            }
            return -1;
        }

        [GoFunc]
        public static Slice<string> FieldsFunc(string s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var result = new System.Collections.Generic.List<string>();
            int start = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (f((long)s[i]))
                {
                    if (start >= 0) { result.Add(s.Substring(start, i - start)); start = -1; }
                }
                else if (start < 0) { start = i; }
            }
            if (start >= 0) result.Add(s.Substring(start));
            return new Slice<string>(result.ToArray());
        }

        [GoFunc]
        public static long LastIndexByte(string s, byte c)
        {
            return s.LastIndexOf((char)c);
        }

        [GoFunc]
        public static long LastIndexAny(string s, string chars)
        {
            return s.LastIndexOfAny(chars.ToCharArray());
        }

        [GoFunc]
        public static long Compare(string a, string b) => string.Compare(a, b, StringComparison.Ordinal);

        [GoFunc]
        public static string Clone(string s) => s;

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (string, bool) CutPrefix(string s, string prefix)
        {
            if (s.StartsWith(prefix))
                return (s.Substring(prefix.Length), true);
            return (s, false);
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (string, bool) CutSuffix(string s, string suffix)
        {
            if (s.EndsWith(suffix))
                return (s.Substring(0, s.Length - suffix.Length), true);
            return (s, false);
        }

        [GoFunc]
        public static string ToTitle(string s) => s.ToUpper();

        [GoFunc]
        public static string ToValidUTF8(string s, string replacement) => s;

        [GoFunc]
        [return: GoReturn("*Reader")]
        public static Reader NewReader(string s)
        {
            Console.Error.WriteLine($"[TRACE] strings.NewReader called, s='{s}'");
            return new Reader(s);
        }

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("*Replacer")]
        public static Replacer NewReplacer(params string[] oldnew)
        {
            return new Replacer(oldnew);
        }

    }
}
