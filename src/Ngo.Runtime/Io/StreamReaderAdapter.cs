using System;
using System.IO;

namespace Ngo.Runtime.Io
{
    /// <summary>
    /// Adapts a .NET Stream to Go's io.Reader interface.
    /// </summary>
    public sealed class StreamReaderAdapter : IGoReader, IGoCloser
    {
        private readonly Stream _stream;

        public StreamReaderAdapter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public (long, string) Read(Slice<byte> p)
        {
            if (p.Len == 0)
            {
                return (0, null!);
            }

            var buffer = new byte[p.Len];
            int n = _stream.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < n; i++)
            {
                p[i] = buffer[i];
            }

            if (n == 0)
            {
                return (0, "EOF");
            }

            return (n, null!);
        }

        public string Close()
        {
            try
            {
                _stream.Close();
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
