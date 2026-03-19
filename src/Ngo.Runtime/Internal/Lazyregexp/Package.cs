using System.Text.RegularExpressions;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Lazyregexp
{
    /// <summary>
    /// Stub for internal/lazyregexp — lazily compiled regular expressions.
    /// </summary>
    [GoPackage("internal/lazyregexp")]
    public static class Package
    {
        // func New(str string) *Regexp
        [GoFunc]
        [return: GoReturn("*internal/lazyregexp.Regexp")]
        public static GoRegexp New(string str) => new GoRegexp(str);
    }

    [GoType("struct", Name = "Regexp", Package = "internal/lazyregexp")]
    public class GoRegexp
    {
        private readonly string _pattern;
        private Regex? _compiled;

        public GoRegexp(string pattern = "") { _pattern = pattern; }

        private Regex Compiled()
        {
            _compiled ??= new Regex(_pattern);
            return _compiled;
        }

        [GoMethod]
        public bool MatchString(string s) => Compiled().IsMatch(s);

        [GoMethod]
        public string ReplaceAllString(string src, string repl) => Compiled().Replace(src, repl);

        [GoMethod]
        [return: GoReturn("[]string")]
        public Slice<string> FindStringSubmatch(string s)
        {
            var m = Compiled().Match(s);
            if (!m.Success) return default;
            var result = new string[m.Groups.Count];
            for (int i = 0; i < m.Groups.Count; i++)
                result[i] = m.Groups[i].Value;
            return new Slice<string>(result);
        }

        [GoMethod]
        [return: GoReturn("[][]string")]
        public Slice<Slice<string>> FindAllStringSubmatch(string s, [GoParam("int")] long n)
        {
            var matches = Compiled().Matches(s);
            var result = new Slice<string>[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var groups = new string[m.Groups.Count];
                for (int j = 0; j < m.Groups.Count; j++)
                    groups[j] = m.Groups[j].Value;
                result[i] = new Slice<string>(groups);
            }
            return new Slice<Slice<string>>(result);
        }

        [GoMethod]
        public string String() => _pattern;
    }
}
