using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Base64
{
    [GoType("struct", Name = "Encoding", Package = "encoding/base64")]
    public class Encoding
    {
        private readonly Func<byte[], string> _encode;
        private readonly Func<string, byte[]> _decode;

        internal Encoding(Func<byte[], string> encode, Func<string, byte[]> decode, bool isURL)
        {
            _encode = encode;
            _decode = decode;
        }

        public string EncodeToString(Slice<byte> src)
        {
            return _encode(src.AsReadOnlySpan().ToArray());
        }

        public (Slice<byte>, object?) DecodeString(string s)
        {
            try
            {
                var bytes = _decode(s);
                return (new Slice<byte>(bytes), null);
            }
            catch (FormatException ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        public long EncodedLen(long n) => (n + 2) / 3 * 4;

        [GoMethod]
        public long DecodedLen(long n) => n / 4 * 3;

        [GoMethod]
        public void Encode(Slice<byte> dst, Slice<byte> src)
        {
            var bytes = new byte[src.Len];
            for (int i = 0; i < src.Len; i++) bytes[i] = src[i];
            var encoded = _encode(bytes);
            for (int i = 0; i < encoded.Length && i < dst.Len; i++)
                dst[i] = (byte)encoded[i];
        }

        [GoMethod]
        public Slice<byte> AppendEncode(Slice<byte> dst, Slice<byte> src)
        {
            var bytes = new byte[src.Len];
            for (int i = 0; i < src.Len; i++) bytes[i] = src[i];
            var encoded = _encode(bytes);
            var result = new byte[dst.Len + encoded.Length];
            for (int i = 0; i < dst.Len; i++) result[i] = dst[i];
            for (int i = 0; i < encoded.Length; i++) result[dst.Len + i] = (byte)encoded[i];
            return new Slice<byte>(result);
        }

        [GoMethod]
        public Slice<byte> AppendDecode(Slice<byte> dst, Slice<byte> src)
        {
            try
            {
                var srcBytes = new byte[src.Len];
                for (int i = 0; i < src.Len; i++) srcBytes[i] = src[i];
                var decoded = _decode(System.Text.Encoding.ASCII.GetString(srcBytes));
                var result = new byte[dst.Len + decoded.Length];
                for (int i = 0; i < dst.Len; i++) result[i] = dst[i];
                for (int i = 0; i < decoded.Length; i++) result[dst.Len + i] = decoded[i];
                return new Slice<byte>(result);
            }
            catch { return dst; }
        }

        [GoMethod]
        [return: GoReturn("*base64.Encoding")]
        public Encoding WithPadding([GoParam("rune")] long padding)
        {
            return this;
        }

        [GoMethod]
        public Encoding Strict()
        {
            // In Go, Strict() returns an Encoding that requires padding.
            // Stub: return self since our implementation doesn't distinguish.
            return this;
        }

        [GoMethod]
        public (long, object?) Decode(Slice<byte> dst, Slice<byte> src)
        {
            try
            {
                var srcBytes = new byte[src.Len];
                for (int i = 0; i < src.Len; i++) srcBytes[i] = src[i];
                var decoded = _decode(System.Text.Encoding.ASCII.GetString(srcBytes));
                int n = global::System.Math.Min(decoded.Length, dst.Len);
                for (int i = 0; i < n; i++) dst[i] = decoded[i];
                return (n, null);
            }
            catch (FormatException ex)
            {
                return (0, ex.Message);
            }
        }
    }
}
