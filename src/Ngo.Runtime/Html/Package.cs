using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Html
{
    [GoPackage("html")]
    public static class Package
    {
        // html.EscapeString(s string) string
        [GoFunc]
        public static string EscapeString(string s) => System.Net.WebUtility.HtmlEncode(s) ?? s;

        // html.UnescapeString(s string) string
        [GoFunc]
        public static string UnescapeString(string s) => System.Net.WebUtility.HtmlDecode(s) ?? s;
    }
}
