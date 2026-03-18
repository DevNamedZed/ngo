using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Request", Package = "net/http")]
    public class Request
    {
        [GoField(Name = "Method")] public string Method { get; set; } = "GET";
        [GoField(Name = "URL", Type = "*url.URL")] public object? URL { get; set; }
        [GoField(Name = "Header")] public Header Header { get; set; } = new Header();
        [GoField(Name = "Body")] public object? Body { get; set; }
        [GoField(Name = "ContentLength")] public long ContentLength { get; set; }
        [GoField(Name = "Host")] public string Host { get; set; } = "";
        [GoField(Name = "Form", Type = "url.Values")] public object? Form { get; set; }
        [GoField(Name = "PostForm", Type = "url.Values")] public object? PostForm { get; set; }
        [GoField(Name = "MultipartForm")] public object? MultipartForm { get; set; }
        [GoField(Name = "RemoteAddr")] public string RemoteAddr { get; set; } = "";
        [GoField(Name = "RequestURI")] public string RequestURI { get; set; } = "";
        [GoField(Name = "Proto")] public string Proto { get; set; } = "HTTP/1.1";
        [GoField(Name = "ProtoMajor")] public long ProtoMajor { get; set; } = 1;
        [GoField(Name = "ProtoMinor")] public long ProtoMinor { get; set; } = 1;
        [GoField(Name = "TLS")] public object? TLS { get; set; }
        [GoField(Name = "Close")] public bool Close { get; set; }
        [GoField(Name = "Trailer")] public Header Trailer { get; set; } = new Header();
        [GoField(Name = "Response")] public object? Response { get; set; }
        [GoField(Name = "TransferEncoding")] public Slice<string> TransferEncoding { get; set; }

        internal string URLPath { get; set; } = "";

        private object? _ctx;

        [GoMethod]
        public Request Clone(object? ctx)
        {
            var clone = (Request)MemberwiseClone();
            clone._ctx = ctx;
            return clone;
        }

        [GoMethod]
        public object? Context()
        {
            if (_ctx != null)
            {
                return _ctx;
            }
            return Ngo.Runtime.Context.GoContext.Background();
        }

        [GoMethod]
        public Request WithContext(object? ctx)
        {
            var clone = (Request)MemberwiseClone();
            clone._ctx = ctx;
            return clone;
        }

        [GoMethod]
        public string FormValue(string key)
        {
            if (Form is Url.GoValues values)
            {
                return values.Get(key);
            }
            // Try parsing URL query
            if (URL is Url.GoURL goUrl && !string.IsNullOrEmpty(goUrl.RawQuery))
            {
                var (parsed, _) = Url.Package.ParseQuery(goUrl.RawQuery);
                if (parsed != null)
                {
                    return parsed.Get(key);
                }
            }
            return "";
        }
        [GoMethod]
        public (object?, object?, object?) FormFile(string key) => (null, null, null);
        [GoMethod]
        [return: GoReturn("*Cookie", "error")]
        public (Cookie?, string) Cookie(string name)
        {
            string cookieHeader = Header.Get("Cookie");
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return (null, "http: named cookie not present");
            }
            foreach (var part in cookieHeader.Split(';'))
            {
                var trimmed = part.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    var cookieName = trimmed.Substring(0, eq).Trim();
                    if (cookieName == name)
                    {
                        var cookieValue = trimmed.Substring(eq + 1).Trim();
                        return (new Cookie { Name = cookieName, Value = cookieValue }, null!);
                    }
                }
            }
            return (null, "http: named cookie not present");
        }

        [GoMethod]
        public Slice<Cookie> Cookies()
        {
            string cookieHeader = Header.Get("Cookie");
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return new Slice<Cookie>();
            }
            var cookies = new System.Collections.Generic.List<Cookie>();
            foreach (var part in cookieHeader.Split(';'))
            {
                var trimmed = part.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    cookies.Add(new Cookie
                    {
                        Name = trimmed.Substring(0, eq).Trim(),
                        Value = trimmed.Substring(eq + 1).Trim(),
                    });
                }
            }
            return new Slice<Cookie>(cookies.ToArray());
        }

        [GoMethod]
        public void AddCookie(Cookie c)
        {
            if (c == null || string.IsNullOrEmpty(c.Name))
            {
                return;
            }
            string existing = Header.Get("Cookie");
            if (string.IsNullOrEmpty(existing))
            {
                Header.Set("Cookie", c.String());
            }
            else
            {
                Header.Set("Cookie", existing + "; " + c.String());
            }
        }
        [GoMethod]
        public string Referer() => Header.Get("Referer");
        [GoMethod]
        public string UserAgent() => Header.Get("User-Agent");
        [GoMethod]
        public (string, string, bool) BasicAuth()
        {
            string auth = Header.Get("Authorization");
            if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Basic ", System.StringComparison.OrdinalIgnoreCase))
            {
                return ("", "", false);
            }
            try
            {
                var decoded = System.Convert.FromBase64String(auth.Substring(6));
                var cred = System.Text.Encoding.UTF8.GetString(decoded);
                int colon = cred.IndexOf(':');
                if (colon < 0)
                {
                    return (cred, "", true);
                }
                return (cred.Substring(0, colon), cred.Substring(colon + 1), true);
            }
            catch
            {
                return ("", "", false);
            }
        }
        [GoMethod]
        public void SetBasicAuth(string username, string password)
        {
            var encoded = System.Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
            Header.Set("Authorization", "Basic " + encoded);
        }
        [GoMethod]
        [return: GoReturn("error")]
        public object? ParseForm()
        {
            if (Form != null)
            {
                return null;
            }

            var values = new Url.GoValues();

            // Parse URL query parameters
            if (URL is Url.GoURL goUrl && !string.IsNullOrEmpty(goUrl.RawQuery))
            {
                var (parsed, _) = Url.Package.ParseQuery(goUrl.RawQuery);
                if (parsed != null)
                {
                    values = parsed;
                }
            }

            // Parse body for POST/PUT/PATCH with form content type
            if (Body is Io.IGoReader reader &&
                (Method == "POST" || Method == "PUT" || Method == "PATCH"))
            {
                string contentType = Header.Get("Content-Type");
                if (contentType.Contains("application/x-www-form-urlencoded"))
                {
                    var (bodyBytes, _) = Io.GoIo.ReadAll(reader);
                    if (bodyBytes.Len > 0)
                    {
                        var bodyStr = System.Text.Encoding.UTF8.GetString(bodyBytes.AsSpan());
                        var (postValues, _) = Url.Package.ParseQuery(bodyStr);
                        if (postValues != null)
                        {
                            PostForm = postValues;
                        }
                    }
                }
            }

            Form = values;
            return null;
        }
        [GoMethod]
        [return: GoReturn("error")]
        public object? ParseMultipartForm(long maxMemory) => null;
        [GoMethod]
        public (object?, object?) MultipartReader() => (null, null);
        [GoMethod]
        [return: GoReturn("error")]
        public object? Write(object? w) => null;
        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteProxy(object? w) => null;
    }
}
