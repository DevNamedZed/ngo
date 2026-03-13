using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Decoder struct
    [GoType("struct", Name = "Decoder", Package = "encoding/xml")]
    public class GoDecoder
    {
        [GoField(Name = "Strict")] public bool Strict;
        [GoField(Name = "AutoClose")] public Slice<string> AutoClose;
        [GoField(Name = "CharsetReader")] public object? CharsetReader;

        private readonly IGoReader? _reader;

        public GoDecoder() { }

        public GoDecoder(IGoReader? reader)
        {
            _reader = reader;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Decode(object? v)
        {
            if (v == null)
            {
                return "xml: Decode target is nil";
            }
            var data = ReadAll();
            if (data.Len == 0)
            {
                return "EOF";
            }
            return Package.Unmarshal(data, v);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? DecodeElement(object? v, [GoParam("*xml.StartElement")] object? start)
        {
            return Decode(v);
        }

        [GoMethod]
        [return: GoReturn("xml.Token", "error")]
        public (object?, object?) Token() => (null, "EOF");

        [GoMethod]
        [return: GoReturn("xml.Token", "error")]
        public (object?, object?) RawToken() => (null, "EOF");

        [GoMethod]
        [return: GoReturn("int64")]
        public long InputOffset() => 0;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Skip() => null;

        private Slice<byte> ReadAll()
        {
            if (_reader == null)
            {
                return new Slice<byte>();
            }

            var sb = new StringBuilder();
            var chunk = new byte[4096];
            var sliceChunk = new Slice<byte>(chunk);
            while (true)
            {
                var (n, err) = _reader.Read(sliceChunk);
                if (n > 0)
                {
                    for (int i = 0; i < (int)n; i++)
                    {
                        sb.Append((char)sliceChunk[i]);
                    }
                }
                if (err != null || n == 0)
                {
                    break;
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return new Slice<byte>(bytes);
        }
    }
}
