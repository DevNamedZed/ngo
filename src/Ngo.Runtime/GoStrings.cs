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
    }
}
