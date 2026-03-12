using System.IO;
using System.Net.Http;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Response", Package = "net/http")]
    public class Response : IGoReader
    {
        private readonly HttpResponseMessage _response;
        private Stream? _bodyStream;

        public Response()
        {
            _response = new HttpResponseMessage();
            StatusCode = 200;
            Status = "200 OK";
        }

        public Response(HttpResponseMessage response)
        {
            _response = response;
            StatusCode = (long)response.StatusCode;
            Status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }

        [GoField(Name = "StatusCode")] public long StatusCode { get; set; }
        [GoField(Name = "Status")] public string Status { get; set; }
        [GoField(Name = "Proto")] public string Proto { get; set; } = "HTTP/1.1";
        [GoField(Name = "ProtoMajor")] public long ProtoMajor { get; set; } = 1;
        [GoField(Name = "ProtoMinor")] public long ProtoMinor { get; set; } = 1;
        [GoField(Name = "Header")] public Header Header { get; set; } = new Header();
        [GoField(Name = "Body")] public object? Body { get; set; }
        [GoField(Name = "ContentLength")] public long ContentLength { get; set; }
        [GoField(Name = "TransferEncoding")] public Slice<string> TransferEncoding { get; set; }
        [GoField(Name = "Close")] public bool Close { get; set; }
        [GoField(Name = "Uncompressed")] public bool Uncompressed { get; set; }
        [GoField(Name = "Trailer")] public Header Trailer { get; set; } = new Header();
        [GoField(Name = "Request")] public Request? Request { get; set; }
        [GoField(Name = "TLS")] public object? TLS { get; set; }

        [GoMethod]
        public Slice<Cookie> Cookies() => new Slice<Cookie>();

        [GoMethod]
        public object? Location() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Write(object? w) => null;

        public (int, string) Read(Slice<byte> p)
        {
            _bodyStream ??= _response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            var buf = new byte[p.Len];
            var n = _bodyStream.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++)
                p[i] = buf[i];
            if (n == 0)
                return (0, "EOF");
            return (n, "");
        }
    }
}
