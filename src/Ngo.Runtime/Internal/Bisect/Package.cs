using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Bisect
{
    /// <summary>
    /// internal/bisect — binary search pattern matcher used by go tools.
    /// </summary>
    [GoPackage("internal/bisect")]
    public static class Package
    {
        [GoConst] public const string Marker = "";

        [GoFunc]
        public static bool Enabled(string pattern) => false;

        [GoFunc]
        [return: GoReturn("*Matcher", "error")]
        public static (object?, object?) New(string pattern) => (new GoMatcher(), null);
    }

    [GoType("struct", Name = "Matcher", Package = "internal/bisect")]
    public class GoMatcher
    {
        [GoMethod] public bool ShouldEnable(long id) => true;
        [GoMethod] public bool ShouldReport(long id) => false;
        [GoMethod] public bool ShouldPrint(long id) => false;
        [GoMethod] public bool MarkerOnly() => false;
    }
}
