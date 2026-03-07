// -----------------------------------------------------------------------
// <copyright file="GoStrings.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoStrings
    {
        public static bool Contains(string s, string substr) => s.Contains(substr);

        public static bool HasPrefix(string s, string prefix) => s.StartsWith(prefix);

        public static bool HasSuffix(string s, string suffix) => s.EndsWith(suffix);

        public static string Join(Slice<string> elems, string sep)
        {
            var parts = new string[elems.Len];
            for (int i = 0; i < elems.Len; i++)
            {
                parts[i] = elems[i];
            }
            return string.Join(sep, parts);
        }

        public static Slice<string> Split(string s, string sep)
        {
            var parts = s.Split(new[] { sep }, StringSplitOptions.None);
            return new Slice<string>(parts);
        }

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

        public static string TrimSpace(string s) => s.Trim();

        public static string ToUpper(string s) => s.ToUpper();

        public static string ToLower(string s) => s.ToLower();

        public static long Index(string s, string substr) => s.IndexOf(substr, StringComparison.Ordinal);

        public static string Repeat(string s, long count)
        {
            if (count <= 0) return "";
            var sb = new System.Text.StringBuilder(s.Length * (int)count);
            for (long i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }

        public static string ReplaceAll(string s, string old, string @new) => s.Replace(old, @new);

        public static string Trim(string s, string cutset) => s.Trim(cutset.ToCharArray());

        public static string TrimPrefix(string s, string prefix) =>
            s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;

        public static string TrimSuffix(string s, string suffix) =>
            s.EndsWith(suffix) ? s.Substring(0, s.Length - suffix.Length) : s;

        public static string TrimLeft(string s, string cutset) => s.TrimStart(cutset.ToCharArray());

        public static string TrimRight(string s, string cutset) => s.TrimEnd(cutset.ToCharArray());

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

        public static bool EqualFold(string s, string t) =>
            string.Equals(s, t, StringComparison.OrdinalIgnoreCase);

        public static Slice<string> Fields(string s)
        {
            var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return new Slice<string>(parts);
        }

        public static long LastIndex(string s, string substr) =>
            (long)s.LastIndexOf(substr, StringComparison.Ordinal);

        public static bool ContainsRune(string s, long r) =>
            s.IndexOf((char)r) >= 0;

        public static bool ContainsAny(string s, string chars)
        {
            foreach (char c in chars)
            {
                if (s.IndexOf(c) >= 0) return true;
            }
            return false;
        }

        public static (string before, string after, bool found) Cut(string s, string sep)
        {
            int idx = s.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0)
            {
                return (s, "", false);
            }
            return (s.Substring(0, idx), s.Substring(idx + sep.Length), true);
        }

        public static string Map(Func<long, long> mapping, string s)
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

        public static Slice<string> SplitN(string s, string sep, long n)
        {
            if (n == 0) return new Slice<string>(Array.Empty<string>());
            var parts = s.Split(new[] { sep }, (int)n, StringSplitOptions.None);
            return new Slice<string>(parts);
        }

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

        public static long IndexByte(string s, byte c)
        {
            return s.IndexOf((char)c);
        }

        public static long IndexRune(string s, long r)
        {
            return s.IndexOf((char)r);
        }

        public static long IndexAny(string s, string chars)
        {
            return s.IndexOfAny(chars.ToCharArray());
        }

        public static string TrimFunc(string s, Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Length && f((long)s[start]))
                start++;
            int end = s.Length;
            while (end > start && f((long)s[end - 1]))
                end--;
            return s.Substring(start, end - start);
        }

        public static string TrimRightFunc(string s, Func<long, bool> f)
        {
            int end = s.Length;
            while (end > 0 && f((long)s[end - 1]))
                end--;
            return s.Substring(0, end);
        }

        public static string TrimLeftFunc(string s, Func<long, bool> f)
        {
            int start = 0;
            while (start < s.Length && f((long)s[start]))
                start++;
            return s.Substring(start);
        }
    }

    public sealed class GoReplacer
    {
        private readonly (string oldVal, string newVal)[] _pairs;

        public GoReplacer(string[] pairs)
        {
            _pairs = new (string, string)[pairs.Length / 2];
            for (int i = 0; i < pairs.Length; i += 2)
            {
                _pairs[i / 2] = (pairs[i], pairs[i + 1]);
            }
        }

        public string Replace(string s)
        {
            foreach (var (oldVal, newVal) in _pairs)
            {
                s = s.Replace(oldVal, newVal);
            }

            return s;
        }
    }
}
