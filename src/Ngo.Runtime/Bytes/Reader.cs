using System;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bytes
{
    [GoType("struct", Name = "Reader", Package = "bytes")]
    public sealed class Reader : IGoReader
    {
        private byte[] _data;
        private int _pos;

        public Reader(Slice<byte> b)
        {
            _data = new byte[b.Len];
            for (int i = 0; i < b.Len; i++)
                _data[i] = b[i];
            _pos = 0;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            if (_pos >= _data.Length) return (0, GoIo.EOF);
            int n = global::System.Math.Min(p.Len, _data.Length - _pos);
            for (int i = 0; i < n; i++)
                p[i] = _data[_pos + i];
            _pos += n;
            return (n, "");
        }

        [GoMethod]
        [return: GoReturn("byte", "error")]
        public (byte, object?) ReadByte()
        {
            if (_pos >= _data.Length) return (0, GoIo.EOF);
            return (_data[_pos++], null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadByte()
        {
            if (_pos <= 0) return "bytes.Reader.UnreadByte: at beginning of slice";
            _pos--;
            return null;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Len() => global::System.Math.Max(0, _data.Length - _pos);

        [GoMethod]
        [return: GoReturn("int64")]
        public long Size() => _data.Length;

        [GoMethod]
        public void Reset(Slice<byte> b)
        {
            _data = new byte[b.Len];
            for (int i = 0; i < b.Len; i++)
                _data[i] = b[i];
            _pos = 0;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, object?) ReadAt(Slice<byte> b, long off)
        {
            if (off < 0) return (0, "bytes.Reader.ReadAt: negative offset");
            if (off >= _data.Length) return (0, GoIo.EOF);
            int n = global::System.Math.Min(b.Len, _data.Length - (int)off);
            for (int i = 0; i < n; i++)
                b[i] = _data[(int)off + i];
            if (n < b.Len) return (n, GoIo.EOF);
            return (n, null);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Seek(long offset, [GoParam("int")] long whence)
        {
            long abs;
            switch (whence)
            {
                case 0: abs = offset; break; // io.SeekStart
                case 1: abs = _pos + offset; break; // io.SeekCurrent
                case 2: abs = _data.Length + offset; break; // io.SeekEnd
                default: return (0, "bytes.Reader.Seek: invalid whence");
            }
            if (abs < 0) return (0, "bytes.Reader.Seek: negative position");
            _pos = (int)abs;
            return (_pos, null);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) WriteTo([GoParam("io.Writer")] object w)
        {
            if (w is IGoWriter writer)
            {
                int remaining = _data.Length - _pos;
                if (remaining <= 0) return (0, null);
                var slice = new Slice<byte>(_data, _pos, remaining);
                var (n, err) = writer.Write(slice);
                _pos += n;
                return (n, string.IsNullOrEmpty(err) ? null : err);
            }
            return (0, "bytes.Reader.WriteTo: invalid writer");
        }

        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        public (long, long, object?) ReadRune()
        {
            if (_pos >= _data.Length) return (0, 0, GoIo.EOF);
            byte b0 = _data[_pos];
            if (b0 < 0x80)
            {
                _pos++;
                return (b0, 1, null);
            }
            int remaining = _data.Length - _pos;
            int size = b0 < 0xE0 ? 2 : b0 < 0xF0 ? 3 : 4;
            if (remaining < size)
            {
                _pos++;
                return (0xFFFD, 1, null);
            }
            var slice = new Slice<byte>(_data, _pos, size);
            var (r, sz) = Utf8.Package.DecodeRune(slice);
            _pos += (int)sz;
            return (r, sz, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadRune()
        {
            // stub
            return "bytes.Reader.UnreadRune: not implemented";
        }
    }
}
