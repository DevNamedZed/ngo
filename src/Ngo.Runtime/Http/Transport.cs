using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Transport", Package = "net/http")]
    public class Transport
    {
        [GoField(Name = "Proxy")] public object? Proxy { get; set; }
        [GoField(Name = "Dial")] public object? Dial { get; set; }
        [GoField(Name = "DialContext")] public object? DialContext { get; set; }
        [GoField(Name = "DialTLS")] public object? DialTLS { get; set; }
        [GoField(Name = "DialTLSContext")] public object? DialTLSContext { get; set; }
        [GoField(Name = "TLSClientConfig")] public object? TLSClientConfig { get; set; }
        [GoField(Name = "TLSHandshakeTimeout")] public long TLSHandshakeTimeout { get; set; }
        [GoField(Name = "MaxIdleConns")] public long MaxIdleConns { get; set; }
        [GoField(Name = "MaxIdleConnsPerHost")] public long MaxIdleConnsPerHost { get; set; }
        [GoField(Name = "MaxConnsPerHost")] public long MaxConnsPerHost { get; set; }
        [GoField(Name = "IdleConnTimeout")] public long IdleConnTimeout { get; set; }
        [GoField(Name = "ResponseHeaderTimeout")] public long ResponseHeaderTimeout { get; set; }
        [GoField(Name = "ExpectContinueTimeout")] public long ExpectContinueTimeout { get; set; }
        [GoField(Name = "WriteBufferSize")] public long WriteBufferSize { get; set; }
        [GoField(Name = "ReadBufferSize")] public long ReadBufferSize { get; set; }
        [GoField(Name = "MaxResponseHeaderBytes")] public long MaxResponseHeaderBytes { get; set; }
        [GoField(Name = "DisableKeepAlives")] public bool DisableKeepAlives { get; set; }
        [GoField(Name = "DisableCompression")] public bool DisableCompression { get; set; }
        [GoField(Name = "ForceAttemptHTTP2")] public bool ForceAttemptHTTP2 { get; set; }

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (object?, object?) RoundTrip(Request req) => (null, null);

        [GoMethod]
        public void CloseIdleConnections() { }

        [GoMethod]
        public void RegisterProtocol(string scheme, object? rt) { }

        [GoMethod]
        public Transport Clone() => new Transport();
    }
}
