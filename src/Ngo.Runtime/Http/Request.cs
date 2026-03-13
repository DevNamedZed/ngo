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

        [GoMethod]
        public Request Clone(object? ctx) => this;
        [GoMethod]
        public object? Context() => new object();
        [GoMethod]
        public Request WithContext(object? ctx) => this;
        [GoMethod]
        public string FormValue(string key) => "";
        [GoMethod]
        public (object?, object?, object?) FormFile(string key) => (null, null, null);
        [GoMethod]
        [return: GoReturn("*Cookie", "error")]
        public (Cookie?, string) Cookie(string name) => (null, "http: named cookie not present");
        [GoMethod]
        public Slice<Cookie> Cookies() => new Slice<Cookie>();
        [GoMethod]
        public void AddCookie(Cookie c) { }
        [GoMethod]
        public string Referer() => "";
        [GoMethod]
        public string UserAgent() => "";
        [GoMethod]
        public (string, string, bool) BasicAuth() => ("", "", false);
        [GoMethod]
        public void SetBasicAuth(string username, string password) { }
        [GoMethod]
        [return: GoReturn("error")]
        public object? ParseForm() => null;
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
