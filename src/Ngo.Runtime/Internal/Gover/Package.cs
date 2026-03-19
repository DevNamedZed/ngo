using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Gover
{
    [GoPackage("internal/gover")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("int")]
        public static long Compare(string x, string y)
        {
            return string.Compare(x, y, System.StringComparison.Ordinal);
        }

        [GoFunc]
        public static bool IsValid(string x) => !string.IsNullOrEmpty(x);

        [GoFunc]
        public static string Lang(string x)
        {
            // Extract "1.22" from "go1.22.6"
            if (x.StartsWith("go")) x = x.Substring(2);
            var dot = x.IndexOf('.', x.IndexOf('.') + 1);
            return dot > 0 ? x.Substring(0, dot) : x;
        }

        [GoFunc]
        public static string Max(string x, string y) => Compare(x, y) >= 0 ? x : y;
    }
}
