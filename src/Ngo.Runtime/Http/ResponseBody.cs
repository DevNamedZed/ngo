using System.IO;
using System.Net.Http;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Http
{
    public class ResponseBody : IGoReader
    {
        private readonly HttpResponseMessage _response;
        private Stream? _stream;

        public ResponseBody(HttpResponseMessage response)
        {
            _response = response;
        }

        public (int, string) Read(Slice<byte> p)
        {
            _stream ??= _response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            var buf = new byte[p.Len];
            var n = _stream.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++)
                p[i] = buf[i];
            if (n == 0)
                return (0, "EOF");
            return (n, "");
        }

        public void Close()
        {
            _stream?.Dispose();
            _response.Dispose();
        }
    }
}
