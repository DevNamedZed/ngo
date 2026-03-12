using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bufio
{
    [GoType("struct", Name = "Writer", Package = "bufio")]
    public sealed class Writer : IGoWriter
    {
        private readonly IGoWriter _writer;
        private readonly byte[] _buf;
        private int _bufLen;

        public Writer(IGoWriter writer, int bufSize = 4096)
        {
            _writer = writer;
            _buf = new byte[bufSize];
            _bufLen = 0;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p)
        {
            int written = 0;
            for (int i = 0; i < p.Len; i++)
            {
                _buf[_bufLen++] = p[i];
                written++;
                if (_bufLen >= _buf.Length)
                {
                    var (_, err) = FlushInternal();
                    if (err != "")
                        return (written, err);
                }
            }
            return (written, "");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Flush()
        {
            var (_, err) = FlushInternal();
            return err;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) WriteString(string s)
        {
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            var (n, err) = Write(slice);
            return (n, err);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string WriteByte(byte c)
        {
            if (_bufLen >= _buf.Length)
            {
                var (_, ferr) = FlushInternal();
                if (ferr != "")
                    return ferr;
            }
            _buf[_bufLen++] = c;
            return "";
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) WriteRune([GoParam("rune")] long r)
        {
            char[] chars;
            if (r < 0x10000)
                chars = new[] { (char)r };
            else
                chars = char.ConvertFromUtf32((int)r).ToCharArray();

            var bytes = global::System.Text.Encoding.UTF8.GetBytes(chars);
            var slice = new Slice<byte>(bytes);
            var (n, err) = Write(slice);
            return (n, err);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Buffered() => _bufLen;

        [GoMethod]
        [return: GoReturn("int")]
        public long Available() => _buf.Length - _bufLen;

        [GoMethod]
        public void Reset([GoParam("interface{}")] IGoWriter w)
        {
            _bufLen = 0;
        }

        private (int, string) FlushInternal()
        {
            if (_bufLen == 0)
                return (0, "");
            var slice = new Slice<byte>(_buf, 0, _bufLen);
            var result = _writer.Write(slice);
            _bufLen = 0;
            return result;
        }
    }
}
