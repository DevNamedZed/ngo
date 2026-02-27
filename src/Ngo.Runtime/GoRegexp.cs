// -----------------------------------------------------------------------
// <copyright file="GoRegexp.cs" company="Ziad">
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
using System.Text.RegularExpressions;

namespace Ngo.Runtime
{
    public static class GoRegexp
    {
        // regexp.Compile(expr string) (*Regexp, error)
        public static (GoRegexpObj, string) Compile(string expr)
        {
            try
            {
                var re = new Regex(expr);
                return (new GoRegexpObj(re), "");
            }
            catch (Exception ex)
            {
                return (GoRegexpObj.Null, ex.Message);
            }
        }

        // regexp.MustCompile(expr string) *Regexp
        public static GoRegexpObj MustCompile(string expr)
        {
            try
            {
                var re = new Regex(expr);
                return new GoRegexpObj(re);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"regexp: Compile(`{expr}`): {ex.Message}");
            }
        }

        // regexp.MatchString(pattern string, s string) (bool, error)
        public static (bool, string) MatchString(string pattern, string s)
        {
            try
            {
                return (Regex.IsMatch(s, pattern), "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }

    /// <summary>
    /// Represents a compiled Go regexp.Regexp object.
    /// </summary>
    public sealed class GoRegexpObj
    {
        private readonly Regex? _re;

        public static readonly GoRegexpObj Null = new GoRegexpObj(null);

        public GoRegexpObj(Regex? re)
        {
            _re = re;
        }

        // MatchString(s string) bool
        public bool MatchString(string s)
        {
            return _re != null && _re.IsMatch(s);
        }

        // FindString(s string) string
        public string FindString(string s)
        {
            if (_re == null) return "";
            var m = _re.Match(s);
            return m.Success ? m.Value : "";
        }

        // FindAllString(s string, n int) []string
        public Slice<string> FindAllString(string s, long n)
        {
            if (_re == null) return new Slice<string>(Array.Empty<string>());
            var matches = _re.Matches(s);
            var results = new List<string>();
            int limit = n < 0 ? matches.Count : Math.Min((int)n, matches.Count);
            for (int i = 0; i < limit; i++)
            {
                results.Add(matches[i].Value);
            }
            return new Slice<string>(results.ToArray());
        }

        // ReplaceAllString(src, repl string) string
        public string ReplaceAllString(string src, string repl)
        {
            if (_re == null) return src;
            return _re.Replace(src, repl);
        }

        // Split(s string, n int) []string
        public Slice<string> Split(string s, long n)
        {
            if (_re == null) return new Slice<string>(new[] { s });
            string[] parts;
            if (n < 0)
                parts = _re.Split(s);
            else
                parts = _re.Split(s, (int)n);
            return new Slice<string>(parts);
        }

        // FindStringSubmatch(s string) []string
        public Slice<string> FindStringSubmatch(string s)
        {
            if (_re == null) return new Slice<string>(Array.Empty<string>());
            var m = _re.Match(s);
            if (!m.Success) return new Slice<string>(Array.Empty<string>());
            var results = new string[m.Groups.Count];
            for (int i = 0; i < m.Groups.Count; i++)
                results[i] = m.Groups[i].Value;
            return new Slice<string>(results);
        }

        // String() string
        public override string ToString()
        {
            return _re?.ToString() ?? "";
        }
    }
}
