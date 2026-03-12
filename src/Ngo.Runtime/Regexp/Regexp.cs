using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Regexp
{
    [GoType("struct", Package = "regexp", Name = "Regexp")]
    public sealed class Regexp
    {
        private readonly Regex? _re;
#pragma warning disable CS0414
        private bool _longest;
#pragma warning restore CS0414

        public static readonly Regexp Null = new Regexp(null);

        public Regexp(Regex? re)
        {
            _re = re;
        }

        [GoMethod]
        public bool MatchString(string s)
        {
            return _re != null && _re.IsMatch(s);
        }

        [GoMethod]
        public string FindString(string s)
        {
            if (_re == null) return "";
            var m = _re.Match(s);
            return m.Success ? m.Value : "";
        }

        [GoMethod]
        [return: GoReturn("[]string")]
        public Slice<string> FindAllString(string s, long n)
        {
            if (_re == null) return new Slice<string>(Array.Empty<string>());
            var matches = _re.Matches(s);
            var results = new List<string>();
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            for (int i = 0; i < limit; i++)
                results.Add(matches[i].Value);
            return new Slice<string>(results.ToArray());
        }

        [GoMethod]
        public string ReplaceAllString(string src, string repl)
        {
            if (_re == null) return src;
            return _re.Replace(src, repl);
        }

        [GoMethod]
        public string ReplaceAllLiteralString(string src, string repl)
        {
            if (_re == null) return src;
            return _re.Replace(src, Regex.Escape(repl));
        }

        [GoMethod]
        [return: GoReturn("[]string")]
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

        [GoMethod]
        [return: GoReturn("[]string")]
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

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindStringIndex(string s)
        {
            if (_re == null) return new Slice<long>(Array.Empty<long>());
            var m = _re.Match(s);
            if (!m.Success) return new Slice<long>(Array.Empty<long>());
            return new Slice<long>(new long[] { m.Index, m.Index + m.Length });
        }

        [GoMethod]
        [return: GoReturn("[][]string")]
        public Slice<Slice<string>> FindAllStringSubmatch(string s, long n)
        {
            if (_re == null) return new Slice<Slice<string>>(Array.Empty<Slice<string>>());
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<string>[limit];
            for (int i = 0; i < limit; i++)
            {
                var m = matches[i];
                var groups = new string[m.Groups.Count];
                for (int j = 0; j < m.Groups.Count; j++)
                    groups[j] = m.Groups[j].Value;
                results[i] = new Slice<string>(groups);
            }
            return new Slice<Slice<string>>(results);
        }

        [GoMethod]
        public string ReplaceAllStringFunc(string src, Func<string, string> repl)
        {
            if (_re == null) return src;
            return _re.Replace(src, m => repl(m.Value));
        }

        [GoMethod]
        [return: GoReturn("[][]int")]
        public Slice<Slice<long>> FindAllStringIndex(string s, long n)
        {
            if (_re == null) return new Slice<Slice<long>>(Array.Empty<Slice<long>>());
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<long>[limit];
            for (int i = 0; i < limit; i++)
            {
                var m = matches[i];
                results[i] = new Slice<long>(new long[] { m.Index, m.Index + m.Length });
            }
            return new Slice<Slice<long>>(results);
        }

        [GoMethod]
        public long NumSubexp()
        {
            if (_re == null) return 0;
            return _re.GetGroupNumbers().Length - 1;
        }

        [GoMethod]
        [return: GoReturn("[]string")]
        public Slice<string> SubexpNames()
        {
            if (_re == null) return new Slice<string>(Array.Empty<string>());
            var names = _re.GetGroupNames();
            var result = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (int.TryParse(names[i], out _))
                    result[i] = "";
                else
                    result[i] = names[i];
            }
            return new Slice<string>(result);
        }

        [GoMethod]
        [return: GoReturn("string", "bool")]
        public (string, bool) LiteralPrefix()
        {
            if (_re == null) return ("", false);
            var pattern = _re.ToString();
            var sb = new StringBuilder();
            bool complete = true;
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '\\' && i + 1 < pattern.Length)
                {
                    char next = pattern[i + 1];
                    if ("dDwWsSbB.+*?^$|[](){}".IndexOf(next) < 0)
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                    complete = false;
                    break;
                }
                if (".+*?^$|[](){}\\".IndexOf(c) >= 0)
                {
                    complete = false;
                    break;
                }
                sb.Append(c);
            }
            return (sb.ToString(), complete);
        }

        [GoMethod]
        public bool Match([GoParam("[]byte")] Slice<byte> b)
        {
            if (_re == null) return false;
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            return _re.IsMatch(s);
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Find([GoParam("[]byte")] Slice<byte> b)
        {
            if (_re == null) return new Slice<byte>(Array.Empty<byte>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var m = _re.Match(s);
            if (!m.Success) return new Slice<byte>(Array.Empty<byte>());
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(m.Value));
        }

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindIndex([GoParam("[]byte")] Slice<byte> b)
        {
            if (_re == null) return new Slice<long>(Array.Empty<long>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var m = _re.Match(s);
            if (!m.Success) return new Slice<long>(Array.Empty<long>());
            int byteStart = global::System.Text.Encoding.UTF8.GetByteCount(s.Substring(0, m.Index));
            int byteEnd = byteStart + global::System.Text.Encoding.UTF8.GetByteCount(m.Value);
            return new Slice<long>(new long[] { byteStart, byteEnd });
        }

        [GoMethod]
        [return: GoReturn("[][]byte")]
        public Slice<Slice<byte>> FindAll([GoParam("[]byte")] Slice<byte> b, long n)
        {
            if (_re == null) return new Slice<Slice<byte>>(Array.Empty<Slice<byte>>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<byte>[limit];
            for (int i = 0; i < limit; i++)
                results[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(matches[i].Value));
            return new Slice<Slice<byte>>(results);
        }

        [GoMethod]
        [return: GoReturn("[][]int")]
        public Slice<Slice<long>> FindAllIndex([GoParam("[]byte")] Slice<byte> b, long n)
        {
            if (_re == null) return new Slice<Slice<long>>(Array.Empty<Slice<long>>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<long>[limit];
            for (int i = 0; i < limit; i++)
            {
                var m = matches[i];
                int byteStart = global::System.Text.Encoding.UTF8.GetByteCount(s.Substring(0, m.Index));
                int byteEnd = byteStart + global::System.Text.Encoding.UTF8.GetByteCount(m.Value);
                results[i] = new Slice<long>(new long[] { byteStart, byteEnd });
            }
            return new Slice<Slice<long>>(results);
        }

        [GoMethod]
        [return: GoReturn("[][]byte")]
        public Slice<Slice<byte>> FindSubmatch([GoParam("[]byte")] Slice<byte> b)
        {
            if (_re == null) return new Slice<Slice<byte>>(Array.Empty<Slice<byte>>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var m = _re.Match(s);
            if (!m.Success) return new Slice<Slice<byte>>(Array.Empty<Slice<byte>>());
            var results = new Slice<byte>[m.Groups.Count];
            for (int i = 0; i < m.Groups.Count; i++)
                results[i] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(m.Groups[i].Value));
            return new Slice<Slice<byte>>(results);
        }

        [GoMethod]
        [return: GoReturn("[][][]byte")]
        public Slice<Slice<Slice<byte>>> FindAllSubmatch([GoParam("[]byte")] Slice<byte> b, long n)
        {
            if (_re == null) return new Slice<Slice<Slice<byte>>>(Array.Empty<Slice<Slice<byte>>>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<Slice<byte>>[limit];
            for (int i = 0; i < limit; i++)
            {
                var m = matches[i];
                var groups = new Slice<byte>[m.Groups.Count];
                for (int j = 0; j < m.Groups.Count; j++)
                    groups[j] = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(m.Groups[j].Value));
                results[i] = new Slice<Slice<byte>>(groups);
            }
            return new Slice<Slice<Slice<byte>>>(results);
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> ReplaceAll([GoParam("[]byte")] Slice<byte> src, [GoParam("[]byte")] Slice<byte> repl)
        {
            if (_re == null) return src;
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(src));
            var r = global::System.Text.Encoding.UTF8.GetString(SliceToArray(repl));
            var result = _re.Replace(s, r);
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(result));
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> ReplaceAllLiteral([GoParam("[]byte")] Slice<byte> src, [GoParam("[]byte")] Slice<byte> repl)
        {
            if (_re == null) return src;
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(src));
            var r = global::System.Text.Encoding.UTF8.GetString(SliceToArray(repl));
            var result = _re.Replace(s, Regex.Escape(r));
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(result));
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> ReplaceAllFunc([GoParam("[]byte")] Slice<byte> src, Func<Slice<byte>, Slice<byte>> repl)
        {
            if (_re == null) return src;
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(src));
            var result = _re.Replace(s, m =>
            {
                var input = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(m.Value));
                var output = repl(input);
                return global::System.Text.Encoding.UTF8.GetString(SliceToArray(output));
            });
            return new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(result));
        }

        [GoMethod(Name = "String")]
        public override string ToString()
        {
            return _re?.ToString() ?? "";
        }

        [GoMethod]
        public void Longest()
        {
            _longest = true;
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> ExpandString([GoParam("[]byte")] Slice<byte> dst, string template, string src, [GoParam("[]int")] Slice<long> match)
        {
            var result = new List<byte>();
            if (dst.Len > 0)
                for (int i = 0; i < dst.Len; i++)
                    result.Add(dst[i]);
            result.AddRange(global::System.Text.Encoding.UTF8.GetBytes(template));
            return new Slice<byte>(result.ToArray());
        }

        [GoMethod]
        public long SubexpIndex(string name)
        {
            if (_re == null) return -1;
            var names = _re.GetGroupNames();
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return i;
            return -1;
        }

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindStringSubmatchIndex(string s)
        {
            if (_re == null) return new Slice<long>(Array.Empty<long>());
            var m = _re.Match(s);
            if (!m.Success) return new Slice<long>(Array.Empty<long>());
            var result = new long[m.Groups.Count * 2];
            for (int i = 0; i < m.Groups.Count; i++)
            {
                if (m.Groups[i].Success)
                {
                    result[i * 2] = m.Groups[i].Index;
                    result[i * 2 + 1] = m.Groups[i].Index + m.Groups[i].Length;
                }
                else
                {
                    result[i * 2] = -1;
                    result[i * 2 + 1] = -1;
                }
            }
            return new Slice<long>(result);
        }

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindSubmatchIndex([GoParam("[]byte")] Slice<byte> b)
        {
            if (_re == null) return new Slice<long>(Array.Empty<long>());
            var s = global::System.Text.Encoding.UTF8.GetString(SliceToArray(b));
            var m = _re.Match(s);
            if (!m.Success) return new Slice<long>(Array.Empty<long>());
            var result = new long[m.Groups.Count * 2];
            for (int i = 0; i < m.Groups.Count; i++)
            {
                if (m.Groups[i].Success)
                {
                    int byteStart = global::System.Text.Encoding.UTF8.GetByteCount(s.Substring(0, m.Groups[i].Index));
                    int byteEnd = byteStart + global::System.Text.Encoding.UTF8.GetByteCount(m.Groups[i].Value);
                    result[i * 2] = byteStart;
                    result[i * 2 + 1] = byteEnd;
                }
                else
                {
                    result[i * 2] = -1;
                    result[i * 2 + 1] = -1;
                }
            }
            return new Slice<long>(result);
        }

        [GoMethod]
        [return: GoReturn("[][]int")]
        public Slice<Slice<long>> FindAllStringSubmatchIndex(string s, long n)
        {
            if (_re == null) return new Slice<Slice<long>>(Array.Empty<Slice<long>>());
            var matches = _re.Matches(s);
            int limit = n < 0 ? matches.Count : global::System.Math.Min((int)n, matches.Count);
            var results = new Slice<long>[limit];
            for (int i = 0; i < limit; i++)
            {
                var m = matches[i];
                var indices = new long[m.Groups.Count * 2];
                for (int j = 0; j < m.Groups.Count; j++)
                {
                    if (m.Groups[j].Success)
                    {
                        indices[j * 2] = m.Groups[j].Index;
                        indices[j * 2 + 1] = m.Groups[j].Index + m.Groups[j].Length;
                    }
                    else
                    {
                        indices[j * 2] = -1;
                        indices[j * 2 + 1] = -1;
                    }
                }
                results[i] = new Slice<long>(indices);
            }
            return new Slice<Slice<long>>(results);
        }

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindReaderIndex(object r)
        {
            return new Slice<long>(Array.Empty<long>());
        }

        [GoMethod]
        [return: GoReturn("[]int")]
        public Slice<long> FindReaderSubmatchIndex(object r)
        {
            return new Slice<long>(Array.Empty<long>());
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Expand([GoParam("[]byte")] Slice<byte> dst, [GoParam("[]byte")] Slice<byte> template,
            [GoParam("[]byte")] Slice<byte> src, [GoParam("[]int")] Slice<long> match)
        {
            var result = new List<byte>();
            if (dst.Len > 0)
                for (int i = 0; i < dst.Len; i++)
                    result.Add(dst[i]);
            for (int i = 0; i < template.Len; i++)
                result.Add(template[i]);
            return new Slice<byte>(result.ToArray());
        }

        [GoMethod]
        [return: GoReturn("*Regexp")]
        public Regexp Copy()
        {
            return this;
        }

        [GoMethod]
        public bool MatchReader(object r)
        {
            return false;
        }

        private static byte[] SliceToArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
                arr[i] = s[i];
            return arr;
        }
    }
}
