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

using System.Text.RegularExpressions;

namespace Ngo.Runtime
{
    public sealed class GoRegexp
    {
        private readonly Regex _regex;

        private GoRegexp(Regex regex)
        {
            _regex = regex;
        }

        public static (GoRegexp, string) Compile(string pattern)
        {
            try
            {
                var regex = new Regex(pattern);
                return (new GoRegexp(regex), "");
            }
            catch (RegexParseException exception)
            {
                return (null!, $"error parsing regexp: {exception.Message}");
            }
        }

        public static GoRegexp MustCompile(string pattern)
        {
            var (regexp, err) = Compile(pattern);
            if (err != "")
            {
                throw new GoPanicException($"regexp: Compile({pattern}): {err}");
            }
            return regexp;
        }

        public static (bool, string) MatchString(string pattern, string input)
        {
            try
            {
                var regex = new Regex(pattern);
                return (regex.IsMatch(input), "");
            }
            catch (RegexParseException exception)
            {
                return (false, $"error parsing regexp: {exception.Message}");
            }
        }

        public bool MatchString(string input)
        {
            return _regex.IsMatch(input);
        }

        public string FindString(string input)
        {
            var match = _regex.Match(input);
            if (!match.Success)
            {
                return "";
            }
            return match.Value;
        }

        public Slice<string> FindAllString(string input, int count)
        {
            var matches = _regex.Matches(input);
            int limit = count < 0 ? matches.Count : System.Math.Min(count, matches.Count);
            var results = new string[limit];
            for (int i = 0; i < limit; i++)
            {
                results[i] = matches[i].Value;
            }
            return new Slice<string>(results);
        }

        public string ReplaceAllString(string input, string replacement)
        {
            return _regex.Replace(input, replacement);
        }

        public Slice<string> Split(string input, int count)
        {
            string[] parts;
            if (count < 0)
            {
                parts = _regex.Split(input);
            }
            else
            {
                parts = _regex.Split(input, count);
            }
            return new Slice<string>(parts);
        }

        public Slice<string> FindStringSubmatch(string input)
        {
            var match = _regex.Match(input);
            if (!match.Success)
            {
                return default;
            }
            var results = new string[match.Groups.Count];
            for (int i = 0; i < match.Groups.Count; i++)
            {
                results[i] = match.Groups[i].Value;
            }
            return new Slice<string>(results);
        }
    }
}
