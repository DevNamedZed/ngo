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
        public static bool Contains(GoString s, GoString substr) =>
            s.ToNetString().Contains(substr.ToNetString());

        [GoFunc]
        public static bool HasPrefix(GoString s, GoString prefix) =>
            s.ToNetString().StartsWith(prefix.ToNetString());

        [GoFunc]
        public static bool HasSuffix(GoString s, GoString suffix) =>
            s.ToNetString().EndsWith(suffix.ToNetString());

        [GoFunc]
        public static GoString Join(Slice<GoString> elems, GoString sep)
        {
            var parts = new string[elems.Len];
            for (int i = 0; i < elems.Len; i++)
            {
                parts[i] = elems[i].ToNetString();
            }
            return GoString.FromNetString(string.Join(sep.ToNetString(), parts));
        }

        [GoFunc]
        public static Slice<GoString> Split(GoString s, GoString sep)
        {
            var parts = s.ToNetString().Split(new[] { sep.ToNetString() }, StringSplitOptions.None);
            var result = new GoString[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = GoString.FromNetString(parts[i]);
            }
            return new Slice<GoString>(result);
        }

        [GoFunc]
        public static GoString Replace(GoString s, GoString old, GoString @new, long n)
        {
            var str = s.ToNetString();
            var oldStr = old.ToNetString();
            var newStr = @new.ToNetString();
            if (n < 0)
            {
                return GoString.FromNetString(str.Replace(oldStr, newStr));
            }
            var result = str;
            for (long i = 0; i < n; i++)
            {
                int idx = result.IndexOf(oldStr, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }
                result = result.Substring(0, idx) + newStr + result.Substring(idx + oldStr.Length);
            }
            return GoString.FromNetString(result);
        }

        [GoFunc]
        public static GoString TrimSpace(GoString s) =>
            GoString.FromNetString(s.ToNetString().Trim());

        [GoFunc]
        public static GoString ToUpper(GoString s) =>
            GoString.FromNetString(s.ToNetString().ToUpper());

        [GoFunc]
        public static GoString ToLower(GoString s) =>
            GoString.FromNetString(s.ToNetString().ToLower());

        [GoFunc]
        public static long Index(GoString s, GoString substr) =>
            s.ToNetString().IndexOf(substr.ToNetString(), StringComparison.Ordinal);

        [GoFunc]
        public static GoString Repeat(GoString s, long count)
        {
            if (count <= 0)
            {
                return default;
            }
            var str = s.ToNetString();
            var sb = new System.Text.StringBuilder(str.Length * (int)count);
            for (long i = 0; i < count; i++)
            {
                sb.Append(str);
            }
            return GoString.FromNetString(sb.ToString());
        }

        [GoFunc]
        public static GoString ReplaceAll(GoString s, GoString old, GoString @new) =>
            GoString.FromNetString(s.ToNetString().Replace(old.ToNetString(), @new.ToNetString()));

        [GoFunc]
        public static GoString Trim(GoString s, GoString cutset) =>
            GoString.FromNetString(s.ToNetString().Trim(cutset.ToNetString().ToCharArray()));

        [GoFunc]
        public static GoString TrimPrefix(GoString s, GoString prefix)
        {
            var str = s.ToNetString();
            var pre = prefix.ToNetString();
            return GoString.FromNetString(str.StartsWith(pre) ? str.Substring(pre.Length) : str);
        }

        [GoFunc]
        public static GoString TrimSuffix(GoString s, GoString suffix)
        {
            var str = s.ToNetString();
            var suf = suffix.ToNetString();
            return GoString.FromNetString(str.EndsWith(suf) ? str.Substring(0, str.Length - suf.Length) : str);
        }

        [GoFunc]
        public static GoString TrimLeft(GoString s, GoString cutset) =>
            GoString.FromNetString(s.ToNetString().TrimStart(cutset.ToNetString().ToCharArray()));

        [GoFunc]
        public static GoString TrimRight(GoString s, GoString cutset) =>
            GoString.FromNetString(s.ToNetString().TrimEnd(cutset.ToNetString().ToCharArray()));

        [GoFunc]
        public static long Count(GoString s, GoString substr)
        {
            var str = s.ToNetString();
            var sub = substr.ToNetString();
            if (string.IsNullOrEmpty(sub))
            {
                return (long)str.Length + 1;
            }
            long count = 0;
            int idx = 0;
            while ((idx = str.IndexOf(sub, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += sub.Length;
            }
            return count;
        }

        [GoFunc]
        public static bool EqualFold(GoString s, GoString t) =>
            string.Equals(s.ToNetString(), t.ToNetString(), StringComparison.OrdinalIgnoreCase);

        [GoFunc]
        public static Slice<GoString> Fields(GoString s)
        {
            var parts = s.ToNetString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var result = new GoString[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = GoString.FromNetString(parts[i]);
            }
            return new Slice<GoString>(result);
        }

        [GoFunc]
        public static long LastIndex(GoString s, GoString substr) =>
            (long)s.ToNetString().LastIndexOf(substr.ToNetString(), StringComparison.Ordinal);

        [GoFunc]
        public static bool ContainsRune(GoString s, [GoParam("rune")] long r) =>
            s.ToNetString().IndexOf((char)r) >= 0;

        [GoFunc]
        public static bool ContainsAny(GoString s, GoString chars)
        {
            var str = s.ToNetString();
            foreach (char c in chars.ToNetString())
            {
                if (str.IndexOf(c) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("string", "string", "bool")]
        public static (GoString before, GoString after, bool found) Cut(GoString s, GoString sep)
        {
            var str = s.ToNetString();
            var sepStr = sep.ToNetString();
            int idx = str.IndexOf(sepStr, StringComparison.Ordinal);
            if (idx < 0)
            {
                return (s, default, false);
            }
            return (GoString.FromNetString(str.Substring(0, idx)),
                    GoString.FromNetString(str.Substring(idx + sepStr.Length)),
                    true);
        }

        [GoFunc]
        public static GoString Map([GoParam("func(rune) rune")] Func<long, long> mapping, GoString s)
        {
            var str = s.ToNetString();
            var sb = new System.Text.StringBuilder(str.Length);
            foreach (var c in str)
            {
                var r = mapping((long)c);
                if (r >= 0)
                {
                    sb.Append((char)r);
                }
            }
            return GoString.FromNetString(sb.ToString());
        }

        [GoFunc]
        public static Slice<GoString> SplitN(GoString s, GoString sep, long n)
        {
            if (n == 0)
            {
                return new Slice<GoString>(Array.Empty<GoString>());
            }
            var parts = s.ToNetString().Split(new[] { sep.ToNetString() }, (int)n, StringSplitOptions.None);
            var result = new GoString[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = GoString.FromNetString(parts[i]);
            }
            return new Slice<GoString>(result);
        }

        [GoFunc]
        public static Slice<GoString> SplitAfter(GoString s, GoString sep)
        {
            var sepStr = sep.ToNetString();
            if (string.IsNullOrEmpty(sepStr))
            {
                return Split(s, sep);
            }
            var str = s.ToNetString();
            var resultList = new System.Collections.Generic.List<GoString>();
            int start = 0;
            while (true)
            {
                int idx = str.IndexOf(sepStr, start, StringComparison.Ordinal);
                if (idx < 0)
                {
                    resultList.Add(GoString.FromNetString(str.Substring(start)));
                    break;
                }
                resultList.Add(GoString.FromNetString(str.Substring(start, idx - start + sepStr.Length)));
                start = idx + sepStr.Length;
            }
            return new Slice<GoString>(resultList.ToArray());
        }

        [GoFunc]
        public static Slice<GoString> SplitAfterN(GoString s, GoString sep, long n)
        {
            if (n == 0)
            {
                return new Slice<GoString>(Array.Empty<GoString>());
            }
            if (n == 1)
            {
                return new Slice<GoString>(new[] { s });
            }
            var all = SplitAfter(s, sep);
            if (n < 0 || all.Len <= (int)n)
            {
                return all;
            }
            var result = new GoString[(int)n];
            for (int i = 0; i < (int)n - 1; i++)
            {
                result[i] = all[i];
            }
            var rest = new System.Text.StringBuilder();
            for (int i = (int)n - 1; i < all.Len; i++)
            {
                rest.Append(all[i].ToNetString());
            }
            result[(int)n - 1] = GoString.FromNetString(rest.ToString());
            return new Slice<GoString>(result);
        }

        [GoFunc]
        public static GoString Title(GoString s)
        {
            var str = s.ToNetString();
            if (string.IsNullOrEmpty(str))
            {
                return s;
            }
            var sb = new System.Text.StringBuilder(str.Length);
            bool prev = true;
            foreach (var c in str)
            {
                sb.Append(prev ? char.ToUpper(c) : c);
                prev = char.IsWhiteSpace(c);
            }
            return GoString.FromNetString(sb.ToString());
        }

        [GoFunc]
        public static long IndexByte(GoString s, byte c)
        {
            return s.ToNetString().IndexOf((char)c);
        }

        [GoFunc]
        public static long IndexRune(GoString s, [GoParam("rune")] long r)
        {
            return s.ToNetString().IndexOf((char)r);
        }

        [GoFunc]
        public static long IndexAny(GoString s, GoString chars)
        {
            return s.ToNetString().IndexOfAny(chars.ToNetString().ToCharArray());
        }

        [GoFunc]
        public static GoString TrimFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            int start = 0;
            while (start < str.Length && f((long)str[start]))
            {
                start++;
            }
            int end = str.Length;
            while (end > start && f((long)str[end - 1]))
            {
                end--;
            }
            return GoString.FromNetString(str.Substring(start, end - start));
        }

        [GoFunc]
        public static GoString TrimRightFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            int end = str.Length;
            while (end > 0 && f((long)str[end - 1]))
            {
                end--;
            }
            return GoString.FromNetString(str.Substring(0, end));
        }

        [GoFunc]
        public static GoString TrimLeftFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            int start = 0;
            while (start < str.Length && f((long)str[start]))
            {
                start++;
            }
            return GoString.FromNetString(str.Substring(start));
        }

        [GoFunc]
        public static long IndexFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            for (int i = 0; i < str.Length; i++)
            {
                if (f((long)str[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        [GoFunc]
        public static long LastIndexFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            for (int i = str.Length - 1; i >= 0; i--)
            {
                if (f((long)str[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        [GoFunc]
        public static Slice<GoString> FieldsFunc(GoString s, [GoParam("func(rune) bool")] Func<long, bool> f)
        {
            var str = s.ToNetString();
            var resultList = new System.Collections.Generic.List<GoString>();
            int start = -1;
            for (int i = 0; i < str.Length; i++)
            {
                if (f((long)str[i]))
                {
                    if (start >= 0)
                    {
                        resultList.Add(GoString.FromNetString(str.Substring(start, i - start)));
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
                resultList.Add(GoString.FromNetString(str.Substring(start)));
            }
            return new Slice<GoString>(resultList.ToArray());
        }

        [GoFunc]
        public static long LastIndexByte(GoString s, byte c)
        {
            return s.ToNetString().LastIndexOf((char)c);
        }

        [GoFunc]
        public static long LastIndexAny(GoString s, GoString chars)
        {
            return s.ToNetString().LastIndexOfAny(chars.ToNetString().ToCharArray());
        }

        [GoFunc]
        public static long Compare(GoString a, GoString b) =>
            string.Compare(a.ToNetString(), b.ToNetString(), StringComparison.Ordinal);

        [GoFunc]
        public static GoString Clone(GoString s) => s;

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (GoString, bool) CutPrefix(GoString s, GoString prefix)
        {
            var str = s.ToNetString();
            var pre = prefix.ToNetString();
            if (str.StartsWith(pre))
            {
                return (GoString.FromNetString(str.Substring(pre.Length)), true);
            }
            return (s, false);
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (GoString, bool) CutSuffix(GoString s, GoString suffix)
        {
            var str = s.ToNetString();
            var suf = suffix.ToNetString();
            if (str.EndsWith(suf))
            {
                return (GoString.FromNetString(str.Substring(0, str.Length - suf.Length)), true);
            }
            return (s, false);
        }

        [GoFunc]
        public static GoString ToTitle(GoString s) =>
            GoString.FromNetString(s.ToNetString().ToUpper());

        [GoFunc]
        public static GoString ToValidUTF8(GoString s, GoString replacement) => s;

        [GoFunc]
        [return: GoReturn("*Reader")]
        public static Reader NewReader(GoString s)
        {
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
