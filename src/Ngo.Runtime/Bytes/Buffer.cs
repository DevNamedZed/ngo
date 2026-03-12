using System;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bytes
{
    [GoType("struct", Name = "Buffer", Package = "bytes")]
    public sealed class Buffer : IGoReader, IGoWriter
    {
        private byte[] _buf = new byte[64];
        private int _len;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p)
        {
            EnsureCapacity(_len + p.Len);
            for (int i = 0; i < p.Len; i++)
                _buf[_len++] = p[i];
            return (p.Len, "");
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteString(string s)
        {
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            EnsureCapacity(_len + bytes.Length);
            Array.Copy(bytes, 0, _buf, _len, bytes.Length);
            _len += bytes.Length;
            return (bytes.Length, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteByte([GoParam("byte")] long c)
        {
            EnsureCapacity(_len + 1);
            _buf[_len++] = (byte)c;
            return null;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteRune([GoParam("rune")] long r)
        {
            // Encode the rune as UTF-8 bytes
            char ch = (char)r;
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(new[] { ch });
            EnsureCapacity(_len + bytes.Length);
            Array.Copy(bytes, 0, _buf, _len, bytes.Length);
            _len += bytes.Length;
            return (bytes.Length, null);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadFrom([GoParam("io.Reader")] object reader)
        {
            if (reader is IGoReader r)
            {
                long total = 0;
                var tmp = new Slice<byte>(new byte[4096]);
                while (true)
                {
                    var (n, err) = r.Read(tmp);
                    if (n > 0)
                    {
                        EnsureCapacity(_len + n);
                        for (int i = 0; i < n; i++)
                            _buf[_len++] = tmp[i];
                        total += n;
                    }
                    if (err is string s && s == GoIo.EOF)
                        break;
                    if (err != null && err is string se && se != "")
                        return (total, err);
                    if (n == 0)
                        break;
                }
                return (total, null);
            }
            return (0, "bytes.Buffer: ReadFrom: invalid reader");
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            if (_len == 0) return (0, GoIo.EOF);
            int n = global::System.Math.Min(p.Len, _len);
            for (int i = 0; i < n; i++)
                p[i] = _buf[i];
            // Shift remaining bytes
            Array.Copy(_buf, n, _buf, 0, _len - n);
            _len -= n;
            return (n, "");
        }

        [GoMethod]
        [return: GoReturn("byte", "error")]
        public (byte, object?) ReadByte()
        {
            if (_len == 0) return (0, GoIo.EOF);
            byte b = _buf[0];
            Array.Copy(_buf, 1, _buf, 0, _len - 1);
            _len--;
            return (b, null);
        }

        [GoMethod]
        [return: GoReturn("rune", "int", "error")]
        public (long, long, object?) ReadRune()
        {
            if (_len == 0) return (0, 0, GoIo.EOF);
            byte b0 = _buf[0];
            if (b0 < 0x80)
            {
                Array.Copy(_buf, 1, _buf, 0, _len - 1);
                _len--;
                return (b0, 1, null);
            }
            int size = b0 < 0xE0 ? 2 : b0 < 0xF0 ? 3 : 4;
            if (_len < size) return (0xFFFD, 1, null);
            var slice = new Slice<byte>(_buf, 0, size);
            var (r, sz) = Utf8.Package.DecodeRune(slice);
            Array.Copy(_buf, (int)sz, _buf, 0, _len - (int)sz);
            _len -= (int)sz;
            return (r, sz, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadByte()
        {
            // stub: Buffer.UnreadByte() error
            return "bytes.Buffer: UnreadByte: not implemented";
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnreadRune()
        {
            // stub: Buffer.UnreadRune() error
            return "bytes.Buffer: UnreadRune: not implemented";
        }

        [GoMethod]
        public void Truncate([GoParam("int")] long n)
        {
            if (n < 0 || n > _len) return;
            _len = (int)n;
        }

        [GoMethod]
        public void Grow([GoParam("int")] long n)
        {
            EnsureCapacity(_len + (int)n);
        }

        [GoMethod]
        public Slice<byte> Next([GoParam("int")] long n)
        {
            int take = global::System.Math.Min((int)n, _len);
            var result = new byte[take];
            Array.Copy(_buf, result, take);
            Array.Copy(_buf, take, _buf, 0, _len - take);
            _len -= take;
            return new Slice<byte>(result);
        }

        [GoMethod]
        public Slice<byte> Bytes()
        {
            var result = new byte[_len];
            Array.Copy(_buf, result, _len);
            return new Slice<byte>(result);
        }

        [GoMethod]
        public string String()
        {
            return global::System.Text.Encoding.UTF8.GetString(_buf, 0, _len);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Len() => _len;

        [GoMethod]
        [return: GoReturn("int")]
        public long Cap() => _buf.Length;

        [GoMethod]
        public void Reset()
        {
            _len = 0;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteTo([GoParam("io.Writer")] object writer)
        {
            if (writer is IGoWriter w)
            {
                var data = Bytes();
                var (n, err) = w.Write(data);
                _len = 0;
                return (n, string.IsNullOrEmpty(err) ? null : err);
            }
            return (0, "bytes.Buffer: WriteTo: invalid writer");
        }

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) ReadString(byte delim)
        {
            for (int i = 0; i < _len; i++)
            {
                if (_buf[i] == delim)
                {
                    var result = global::System.Text.Encoding.UTF8.GetString(_buf, 0, i + 1);
                    Array.Copy(_buf, i + 1, _buf, 0, _len - (i + 1));
                    _len -= (i + 1);
                    return (result, null);
                }
            }
            var all = global::System.Text.Encoding.UTF8.GetString(_buf, 0, _len);
            _len = 0;
            return (all, GoIo.EOF);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) ReadBytes(byte delim)
        {
            for (int i = 0; i < _len; i++)
            {
                if (_buf[i] == delim)
                {
                    var result = new byte[i + 1];
                    Array.Copy(_buf, result, i + 1);
                    Array.Copy(_buf, i + 1, _buf, 0, _len - (i + 1));
                    _len -= (i + 1);
                    return (new Slice<byte>(result), null);
                }
            }
            var all = new byte[_len];
            Array.Copy(_buf, all, _len);
            _len = 0;
            return (new Slice<byte>(all), GoIo.EOF);
        }

        [GoMethod]
        public Slice<byte> AvailableBuffer()
        {
            return new Slice<byte>(Array.Empty<byte>());
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Available() => _buf.Length - _len;

        public override string ToString() => String();

        private void EnsureCapacity(int needed)
        {
            if (needed <= _buf.Length) return;
            int newCap = _buf.Length * 2;
            if (newCap < needed) newCap = needed;
            var newBuf = new byte[newCap];
            Array.Copy(_buf, newBuf, _len);
            _buf = newBuf;
        }
    }
}
