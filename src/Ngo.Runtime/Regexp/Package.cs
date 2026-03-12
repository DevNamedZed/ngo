using System;
using System.Text.RegularExpressions;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Regexp
{
    [GoPackage("regexp")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Regexp", "error")]
        public static (Regexp, object?) Compile(string expr)
        {
            try
            {
                var re = new Regex(expr);
                return (new Regexp(re), null);
            }
            catch (Exception ex)
            {
                return (Regexp.Null, Ngo.Runtime.Errors.Package.New(ex.Message));
            }
        }

        [GoFunc]
        [return: GoReturn("*Regexp")]
        public static Regexp MustCompile(string expr)
        {
            try
            {
                var re = new Regex(expr);
                return new Regexp(re);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"regexp: Compile(`{expr}`): {ex.Message}");
            }
        }

        [GoFunc]
        [return: GoReturn("bool", "error")]
        public static (bool, object?) MatchString(string pattern, string s)
        {
            try
            {
                return (Regex.IsMatch(s, pattern), null);
            }
            catch (Exception ex)
            {
                return (false, Ngo.Runtime.Errors.Package.New(ex.Message));
            }
        }

        [GoFunc]
        public static string QuoteMeta(string s)
        {
            return Regex.Escape(s);
        }
    }
}
