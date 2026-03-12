using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Client", Package = "net/http")]
    public class Client
    {
        [GoField(Name = "Transport", Type = "RoundTripper")] public object? Transport { get; set; }
        [GoField(Name = "Timeout")] public long Timeout { get; set; }
        [GoField(Name = "Jar")] public object? Jar { get; set; }
        [GoField(Name = "CheckRedirect")] public object? CheckRedirect { get; set; }

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Get(string url) => Package.Get(url);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Do(Request req) => (new Response(), null);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Post(string url, string contentType, object? body) => Package.Post(url, contentType, body);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Head(string url) => (new Response(), null);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) PostForm(string url, object? data) => (new Response(), null);

        [GoMethod]
        public void CloseIdleConnections() { }
    }
}
