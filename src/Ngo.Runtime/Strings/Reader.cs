using System;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Strings
{
    [GoType("struct", Name = "Reader", Package = "strings")]
    public sealed class Reader : IGoReader, IGoReaderAt, IGoSeeker, IGoByteReader
    {
        private byte[] _data;
        private int _pos;

        public Reader(string s)
        {
            _data = global::System.Text.Encoding.UTF8.GetBytes(s ?? "");
            _pos = 0;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) Read([GoParam("[]byte")] Slice<byte> p)
        {
            if (_pos >= _data.Length)
            {
                return (0, GoIo.EOF);
            }
            int n = global::System.Math.Min(p.Len, _data.Length - _pos);
            for (int i = 0; i < n; i++)
            {
                p[i] = _data[_pos + i];
            }
            _pos += n;
            return (n, "");
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) ReadAt([GoParam("[]byte")] Slice<byte> p, long off)
        {
            if (off >= _data.Length)
            {
                return (0, GoIo.EOF);
            }
            int start = (int)off;
            int n = global::System.Math.Min(p.Len, _data.Length - start);
            for (int j = 0; j < n; j++)
            {
                p[j] = _data[start + j];
            }
            return (n, n < p.Len ? GoIo.EOF : "");
        }

        [GoMethod]
        [return: GoReturn("byte", "error")]
        public (byte, string) ReadByte()
        {
            if (_pos >= _data.Length)
            {
                return (0, GoIo.EOF);
            }
            byte b = _data[_pos];
            _pos++;
            return (b, "");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string UnreadByte()
        {
            if (_pos <= 0)
            {
                return "strings.Reader.UnreadByte: at beginning of string";
            }
            _pos--;
            return "";
        }

        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        public (long, long, string) ReadRune()
        {
            if (_pos >= _data.Length)
            {
                return (0, 0, GoIo.EOF);
            }
            byte b = _data[_pos];
            if (b < 0x80)
            {
                _pos++;
                return ((long)b, 1, "");
            }
            var remaining = _data.AsSpan(_pos);
            var status = System.Text.Rune.DecodeFromUtf8(remaining, out var rune, out int bytesConsumed);
            if (status != System.Buffers.OperationStatus.Done)
            {
                _pos++;
                return (0xFFFD, 1, "");
            }
            _pos += bytesConsumed;
            return (rune.Value, bytesConsumed, "");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string UnreadRune()
        {
            if (_pos <= 0)
            {
                return "strings.Reader.UnreadRune: at beginning of string";
            }
            _pos--;
            while (_pos > 0 && (_data[_pos] & 0xC0) == 0x80)
            {
                _pos--;
            }
            return "";
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, string) Seek(long offset, long whence)
        {
            long abs;
            switch (whence)
            {
                case 0: abs = offset; break;
                case 1: abs = _pos + offset; break;
                case 2: abs = _data.Length + offset; break;
                default: return (0, "strings.Reader.Seek: invalid whence");
            }
            if (abs < 0)
            {
                return (0, "strings.Reader.Seek: negative position");
            }
            _pos = (int)abs;
            return (abs, "");
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, string) WriteTo([GoParam("io.Writer")] object w)
        {
            if (w is IGoWriter writer && _pos < _data.Length)
            {
                var remaining = new Slice<byte>(_data, _pos, _data.Length - _pos);
                var (n, err) = writer.Write(remaining);
                _pos += (int)n;
                return (n, err);
            }
            return (_data.Length - _pos, "");
        }

        [GoMethod]
        public long Len() => _data.Length - _pos;

        [GoMethod]
        public long Size() => _data.Length;

        [GoMethod]
        public void Reset(string s)
        {
            _data = global::System.Text.Encoding.UTF8.GetBytes(s ?? "");
            _pos = 0;
        }
    }
}
