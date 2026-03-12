using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoPackage("net/url")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*URL", "error")]
        public static (GoURL?, object?) Parse(string rawURL)
        {
            try
            {
                var uri = new System.Uri(rawURL, System.UriKind.RelativeOrAbsolute);
                var u = new GoURL();
                if (uri.IsAbsoluteUri)
                {
                    u.Scheme = uri.Scheme;
                    u.Host = uri.Authority;
                    u.Path = uri.AbsolutePath;
                    u.RawQuery = uri.Query.TrimStart('?');
                    u.Fragment = uri.Fragment.TrimStart('#');
                }
                else
                {
                    u.Path = rawURL;
                }
                return (u, null);
            }
            catch (System.Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) PathUnescape(string s)
        {
            try { return (System.Uri.UnescapeDataString(s), null); }
            catch (System.Exception ex) { return ("", ex.Message); }
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) QueryUnescape(string s)
        {
            try { return (System.Uri.UnescapeDataString(s.Replace('+', ' ')), null); }
            catch (System.Exception ex) { return ("", ex.Message); }
        }

        [GoFunc]
        public static string PathEscape(string s) => System.Uri.EscapeDataString(s);

        [GoFunc]
        public static string QueryEscape(string s) => System.Uri.EscapeDataString(s);

        [GoFunc]
        [return: GoReturn("*URL", "error")]
        public static (GoURL?, object?) ParseRequestURI(string rawURL) => Parse(rawURL);

        [GoFunc]
        [return: GoReturn("Values", "error")]
        public static (GoValues?, object?) ParseQuery(string query)
        {
            var values = new GoValues();
            if (string.IsNullOrEmpty(query)) return (values, null);
            foreach (var pair in query.Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq >= 0)
                    values.Add(pair.Substring(0, eq), pair.Substring(eq + 1));
                else
                    values.Add(pair, "");
            }
            return (values, null);
        }

        [GoFunc]
        [return: GoReturn("*Userinfo")]
        public static GoUserinfo User(string username) => new GoUserinfo { username = username };

        [GoFunc]
        [return: GoReturn("*Userinfo")]
        public static GoUserinfo UserPassword(string username, string password)
            => new GoUserinfo { username = username, password = password, passwordSet = true };
    }
}
