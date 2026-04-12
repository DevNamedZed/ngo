using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Stringslite
{
    /// <summary>
    /// internal/stringslite — subset of strings used by low-level packages.
    /// Runtime intrinsic because the Go source uses unsafe.String(&amp;b[0], len)
    /// which cannot be correctly compiled (Ptr&lt;T&gt; loses array backing).
    /// </summary>
    [GoPackage("internal/stringslite")]
    public static class Package
    {
        [GoFunc]
        public static bool HasPrefix(string s, string prefix)
        {
            return s.Length >= prefix.Length
                && s.AsSpan(0, prefix.Length).SequenceEqual(prefix.AsSpan());
        }

        [GoFunc]
        public static bool HasSuffix(string s, string suffix)
        {
            return s.Length >= suffix.Length
                && s.AsSpan(s.Length - suffix.Length).SequenceEqual(suffix.AsSpan());
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long IndexByte(string s, byte c)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if ((byte)s[i] == c)
                {
                    return i;
                }
            }
            return -1;
        }

        [GoFunc]
        [return: GoReturn("int")]
        public static long Index(string s, string substr)
        {
            if (substr.Length == 0)
            {
                return 0;
            }
            return s.IndexOf(substr, StringComparison.Ordinal);
        }

        [GoFunc]
        [return: GoReturn("string", "string", "bool")]
        public static (string before, string after, bool found) Cut(string s, string sep)
        {
            int index = s.IndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
            {
                return (s, "", false);
            }
            return (s.Substring(0, index), s.Substring(index + sep.Length), true);
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (string after, bool found) CutPrefix(string s, string prefix)
        {
            if (!HasPrefix(s, prefix))
            {
                return (s, false);
            }
            return (s.Substring(prefix.Length), true);
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (string before, bool found) CutSuffix(string s, string suffix)
        {
            if (!HasSuffix(s, suffix))
            {
                return (s, false);
            }
            return (s.Substring(0, s.Length - suffix.Length), true);
        }

        [GoFunc]
        public static string TrimPrefix(string s, string prefix)
        {
            if (HasPrefix(s, prefix))
            {
                return s.Substring(prefix.Length);
            }
            return s;
        }

        [GoFunc]
        public static string TrimSuffix(string s, string suffix)
        {
            if (HasSuffix(s, suffix))
            {
                return s.Substring(0, s.Length - suffix.Length);
            }
            return s;
        }

        [GoFunc]
        public static string Clone(string s)
        {
            if (s.Length == 0)
            {
                return "";
            }
            return new string(s.AsSpan());
        }
    }
}
