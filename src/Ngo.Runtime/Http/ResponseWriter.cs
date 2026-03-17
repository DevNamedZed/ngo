using System.IO;
using System.Net;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    public class ResponseWriter : IResponseWriter
    {
        private Header _header = new Header();
        private bool _headerWritten;
        private long _statusCode = 200;
        private readonly MemoryStream _buffer = new MemoryStream();
        internal HttpListenerResponse? ListenerResponse;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p)
        {
            if (!_headerWritten)
            {
                WriteHeader(200);
            }
            for (int i = 0; i < p.Len; i++)
            {
                _buffer.WriteByte(p[i]);
            }
            return (p.Len, null!);
        }

        [GoMethod]
        public void WriteHeader(long statusCode)
        {
            if (_headerWritten)
            {
                return;
            }
            _statusCode = statusCode;
            _headerWritten = true;
        }

        [GoMethod]
        public Header Header() => _header;

        internal void Flush()
        {
            if (ListenerResponse == null)
            {
                return;
            }

            ListenerResponse.StatusCode = (int)_statusCode;

            // Copy headers
            foreach (var kv in _header._values)
            {
                string key = kv.Key;
                var values = kv.Value;
                if (key.Equals("Content-Type", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (values.Len > 0)
                    {
                        ListenerResponse.ContentType = values[0];
                    }
                    continue;
                }
                for (int i = 0; i < values.Len; i++)
                {
                    ListenerResponse.Headers[key] = values[i];
                }
            }

            // Write body
            _buffer.Position = 0;
            ListenerResponse.ContentLength64 = _buffer.Length;
            _buffer.CopyTo(ListenerResponse.OutputStream);
            ListenerResponse.OutputStream.Close();
        }
    }
}
