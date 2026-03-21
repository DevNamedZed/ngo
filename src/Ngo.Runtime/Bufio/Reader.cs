using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bufio
{
    [GoType("struct", Name = "Reader", Package = "bufio")]
    public sealed class Reader : IGoReader
    {
        private readonly IGoReader _reader;
        private readonly byte[] _buf;
        private int _bufLen;
        private int _bufPos;
        private int _lastRuneSize;

        public Reader(IGoReader reader, int bufSize = 4096)
        {
            _reader = reader;
            _buf = new byte[bufSize];
            _bufLen = 0;
            _bufPos = 0;
            _lastRuneSize = -1;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            if (_bufPos >= _bufLen)
            {
                var slice = new Slice<byte>(_buf);
                var (n, err) = _reader.Read(slice);
                if (n == 0)
                    return (0, err);
                _bufLen = n;
                _bufPos = 0;
            }

            int count = global::System.Math.Min(p.Len, _bufLen - _bufPos);
            for (int i = 0; i < count; i++)
                p[i] = _buf[_bufPos + i];
            _bufPos += count;
            _lastRuneSize = -1;
            return (count, null!);
        }

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, string?) ReadString(byte delim)
        {
            var result = new List<byte>();
            while (true)
            {
                if (_bufPos >= _bufLen)
                {
                    var slice = new Slice<byte>(_buf);
                    var (n, err) = _reader.Read(slice);
                    if (n == 0)
                    {
                        if (result.Count > 0)
                            return (global::System.Text.Encoding.UTF8.GetString(result.ToArray()), err);
                        return ("", err);
                    }
                    _bufLen = n;
                    _bufPos = 0;
                }

                byte b = _buf[_bufPos++];
                result.Add(b);
                if (b == delim)
                    return (global::System.Text.Encoding.UTF8.GetString(result.ToArray()), null);
            }
        }

        [GoMethod]
        [return: GoReturn("[]byte", "bool", "error")]
        public (Slice<byte>, bool, string?) ReadLine()
        {
            var result = new List<byte>();
            while (true)
            {
                if (_bufPos >= _bufLen)
                {
                    var slice = new Slice<byte>(_buf);
                    var (n, err) = _reader.Read(slice);
                    if (n == 0)
                    {
                        if (result.Count > 0)
                            return (new Slice<byte>(result.ToArray()), false, err);
                        return (default(Slice<byte>), false, err);
                    }
                    _bufLen = n;
                    _bufPos = 0;
                }

                byte b = _buf[_bufPos++];
                if (b == (byte)'\n')
                {
                    if (result.Count > 0 && result[result.Count - 1] == (byte)'\r')
                        result.RemoveAt(result.Count - 1);
                    return (new Slice<byte>(result.ToArray()), false, null);
                }
                result.Add(b);
            }
        }

        [GoMethod]
        [return: GoReturn("byte", "error")]
        public (byte, string?) ReadByte()
        {
            if (_bufPos >= _bufLen)
            {
                var slice = new Slice<byte>(_buf);
                var (n, err) = _reader.Read(slice);
                if (n == 0)
                    return (0, err != null ? err : "EOF");
                _bufLen = n;
                _bufPos = 0;
            }

            _lastRuneSize = -1;
            return (_buf[_bufPos++], null);
        }

        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        public (long, long, string?) ReadRune()
        {
            var (b, err) = ReadByte();
            if (err != null)
                return (0, 0, err);

            if (b < 0x80)
            {
                _lastRuneSize = 1;
                return (b, 1, null);
            }

            int size;
            long rune;
            if ((b & 0xE0) == 0xC0) { size = 2; rune = b & 0x1F; }
            else if ((b & 0xF0) == 0xE0) { size = 3; rune = b & 0x0F; }
            else if ((b & 0xF8) == 0xF0) { size = 4; rune = b & 0x07; }
            else { _lastRuneSize = 1; return (0xFFFD, 1, null); }

            for (int i = 1; i < size; i++)
            {
                var (cb, cerr) = ReadByte();
                if (cerr != null)
                    return (0xFFFD, i, cerr);
                rune = (rune << 6) | (long)(cb & 0x3F);
            }

            _lastRuneSize = size;
            return (rune, size, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string? UnreadByte()
        {
            if (_bufPos <= 0)
                return "bufio: invalid use of UnreadByte";
            _bufPos--;
            _lastRuneSize = -1;
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string? UnreadRune()
        {
            if (_lastRuneSize < 0)
                return "bufio: invalid use of UnreadRune";
            _bufPos -= _lastRuneSize;
            if (_bufPos < 0) _bufPos = 0;
            _lastRuneSize = -1;
            return null;
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string?) Peek([GoParam("int")] long n)
        {
            int count = (int)n;
            if (_bufPos >= _bufLen)
            {
                var slice = new Slice<byte>(_buf);
                var (nr, err) = _reader.Read(slice);
                if (nr == 0)
                    return (default(Slice<byte>), err != null ? err : "EOF");
                _bufLen = nr;
                _bufPos = 0;
            }

            int available = _bufLen - _bufPos;
            if (count > available)
            {
                var result = new byte[available];
                Array.Copy(_buf, _bufPos, result, 0, available);
                return (new Slice<byte>(result), "bufio: buffer full");
            }

            var peek = new byte[count];
            Array.Copy(_buf, _bufPos, peek, 0, count);
            return (new Slice<byte>(peek), null);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Buffered() => _bufLen - _bufPos;

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => _buf.Length;

        [GoMethod]
        public void Reset([GoParam("interface{}")] IGoReader r)
        {
            _bufLen = 0;
            _bufPos = 0;
            _lastRuneSize = -1;
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string?) ReadBytes(byte delim)
        {
            var result = new List<byte>();
            while (true)
            {
                if (_bufPos >= _bufLen)
                {
                    var slice = new Slice<byte>(_buf);
                    var (n, err) = _reader.Read(slice);
                    if (n == 0)
                    {
                        if (result.Count > 0)
                            return (new Slice<byte>(result.ToArray()), err);
                        return (default(Slice<byte>), err);
                    }
                    _bufLen = n;
                    _bufPos = 0;
                }

                byte b = _buf[_bufPos++];
                result.Add(b);
                if (b == delim)
                    return (new Slice<byte>(result.ToArray()), null);
            }
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string?) ReadSlice(byte delim) => ReadBytes(delim);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string?) Discard([GoParam("int")] long n)
        {
            long discarded = 0;
            while (discarded < n)
            {
                if (_bufPos >= _bufLen)
                {
                    var slice = new Slice<byte>(_buf);
                    var (nr, err) = _reader.Read(slice);
                    if (nr == 0)
                        return (discarded, err != null ? err : "EOF");
                    _bufLen = nr;
                    _bufPos = 0;
                }

                long remaining = n - discarded;
                int available = _bufLen - _bufPos;
                int skip = (int)global::System.Math.Min(remaining, available);
                _bufPos += skip;
                discarded += skip;
            }
            return (discarded, null);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, string?) WriteTo([GoParam("interface{}")] IGoWriter w)
        {
            long total = 0;
            while (_bufPos < _bufLen)
            {
                var slice = new Slice<byte>(_buf, _bufPos, _bufLen - _bufPos);
                var (n, err) = w.Write(slice);
                total += n;
                _bufPos += n;
                if (err != null)
                    return (total, err);
            }

            while (true)
            {
                var readSlice = new Slice<byte>(_buf);
                var (nr, rerr) = _reader.Read(readSlice);
                if (nr == 0)
                {
                    if (rerr != null && rerr != "EOF")
                        return (total, rerr);
                    return (total, null);
                }

                var writeSlice = new Slice<byte>(_buf, 0, nr);
                var (nw, werr) = w.Write(writeSlice);
                total += nw;
                if (werr != null)
                    return (total, werr);
            }
        }
    }
}
