using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Http.Internal
{
    [GoPackage("net/http/internal")]
    public static class Package
    {
        [GoVar(Type = "error")]
        public static readonly object ErrLineTooLong = new Exception("header line too long");

        [GoType("struct", Name = "FlushAfterChunkWriter", Package = "net/http/internal")]
        public class GoFlushAfterChunkWriter
        {
            [GoField(Type = "*bufio.Writer", Embedded = true)] public object? Writer;
        }

        [GoFunc]
        [return: GoReturn("io.Reader")]
        public static object NewChunkedReader([GoParam("io.Reader")] object? r)
        {
            if (r is IGoReader reader)
            {
                return new ChunkedReader(reader);
            }
            return new ChunkedReader(null);
        }

        [GoFunc]
        [return: GoReturn("io.WriteCloser")]
        public static object NewChunkedWriter([GoParam("io.Writer")] object? w)
        {
            if (w is IGoWriter writer)
            {
                return new ChunkedWriter(writer);
            }
            return new ChunkedWriter(null);
        }
    }

    internal class ChunkedReader : IGoReader
    {
        private readonly IGoReader? _reader;
        private long _chunkRemaining;
        private bool _eof;
        private readonly List<byte> _lineBuf = new List<byte>();

        public ChunkedReader(IGoReader? reader)
        {
            _reader = reader;
        }

        public (int, string) Read(Slice<byte> p)
        {
            if (_eof || _reader == null)
            {
                return (0, "EOF");
            }

            int totalRead = 0;
            while (totalRead < p.Len)
            {
                if (_chunkRemaining <= 0)
                {
                    // Read next chunk size line
                    string line = ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        _eof = true;
                        return totalRead > 0 ? (totalRead, null!) : (0, "EOF");
                    }

                    // Parse hex chunk size
                    int semi = line.IndexOf(';');
                    string sizeStr = semi >= 0 ? line.Substring(0, semi) : line;
                    if (!long.TryParse(sizeStr.Trim(), System.Globalization.NumberStyles.HexNumber, null, out _chunkRemaining))
                    {
                        _eof = true;
                        return totalRead > 0 ? (totalRead, null!) : (0, "EOF");
                    }

                    if (_chunkRemaining == 0)
                    {
                        _eof = true;
                        ReadLine(); // trailing CRLF
                        return totalRead > 0 ? (totalRead, null!) : (0, "EOF");
                    }
                }

                int toRead = (int)global::System.Math.Min(p.Len - totalRead, _chunkRemaining);
                var buf = new Slice<byte>(new byte[toRead]);
                var (n, err) = _reader.Read(buf);
                for (int i = 0; i < n; i++)
                {
                    p[totalRead + i] = buf[i];
                }
                totalRead += n;
                _chunkRemaining -= n;

                if (_chunkRemaining == 0)
                {
                    ReadLine(); // trailing CRLF after chunk data
                }

                if (err != null)
                {
                    return (totalRead, err);
                }
            }
            return (totalRead, null!);
        }

        private string ReadLine()
        {
            if (_reader == null)
            {
                return "";
            }
            _lineBuf.Clear();
            var singleByte = new Slice<byte>(new byte[1]);
            while (true)
            {
                var (n, err) = _reader.Read(singleByte);
                if (n == 0 || err != null)
                {
                    break;
                }
                byte b = singleByte[0];
                if (b == '\n')
                {
                    break;
                }
                if (b != '\r')
                {
                    _lineBuf.Add(b);
                }
            }
            return System.Text.Encoding.ASCII.GetString(_lineBuf.ToArray());
        }
    }

    internal class ChunkedWriter : IGoWriter, IGoCloser
    {
        private readonly IGoWriter? _writer;

        public ChunkedWriter(IGoWriter? writer)
        {
            _writer = writer;
        }

        public (int, string) Write(Slice<byte> p)
        {
            if (_writer == null || p.Len == 0)
            {
                return (0, null!);
            }

            // Write chunk header: hex size + CRLF
            var header = System.Text.Encoding.ASCII.GetBytes($"{p.Len:x}\r\n");
            _writer.Write(new Slice<byte>(header));

            // Write chunk data
            var (n, err) = _writer.Write(p);

            // Write trailing CRLF
            _writer.Write(new Slice<byte>(System.Text.Encoding.ASCII.GetBytes("\r\n")));

            return (n, err);
        }

        public string Close()
        {
            if (_writer != null)
            {
                // Write final zero-length chunk
                _writer.Write(new Slice<byte>(System.Text.Encoding.ASCII.GetBytes("0\r\n\r\n")));
            }
            return null!;
        }
    }
}
