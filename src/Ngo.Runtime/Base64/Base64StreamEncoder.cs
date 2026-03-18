using System;
using System.Collections.Generic;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Base64
{
    /// <summary>
    /// Streaming base64 encoder that wraps an io.Writer.
    /// Data written to this encoder is base64-encoded and written to the underlying writer.
    /// Close() flushes any remaining buffered data with proper padding.
    /// </summary>
    internal class Base64StreamEncoder : IGoWriter, IGoCloser
    {
        private readonly Encoding _encoding;
        private readonly IGoWriter? _writer;
        private readonly List<byte> _buffer = new List<byte>();

        public Base64StreamEncoder(Encoding encoding, IGoWriter? writer)
        {
            _encoding = encoding;
            _writer = writer;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                _buffer.Add(p[i]);
            }

            // Encode complete 3-byte groups
            int fullGroups = _buffer.Count / 3 * 3;
            if (fullGroups > 0 && _writer != null)
            {
                var toEncode = new byte[fullGroups];
                _buffer.CopyTo(0, toEncode, 0, fullGroups);
                var encoded = _encoding.EncodeToString(new Slice<byte>(toEncode));
                _writer.Write(new Slice<byte>(System.Text.Encoding.ASCII.GetBytes(encoded)));
                _buffer.RemoveRange(0, fullGroups);
            }

            return (p.Len, null!);
        }

        public string Close()
        {
            // Flush remaining bytes with padding
            if (_buffer.Count > 0 && _writer != null)
            {
                var remaining = _buffer.ToArray();
                var encoded = _encoding.EncodeToString(new Slice<byte>(remaining));
                _writer.Write(new Slice<byte>(System.Text.Encoding.ASCII.GetBytes(encoded)));
                _buffer.Clear();
            }
            return null!;
        }
    }
}
