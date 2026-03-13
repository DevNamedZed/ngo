using System;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Base32
{
    [GoPackage("encoding/base32")]
    public static class Package
    {
        // base32.StdEncoding *Encoding
        [GoVar]
        public static readonly Encoding StdEncoding = new Encoding("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567");

        // base32.HexEncoding *Encoding
        [GoVar]
        public static readonly Encoding HexEncoding = new Encoding("0123456789ABCDEFGHIJKLMNOPQRSTUV");

        // base32.NoPadding rune
        [GoConst]
        public const long NoPadding = -1;

        // base32.StdPadding rune
        [GoConst]
        public const long StdPadding = '=';

        // base32.NewEncoding(encoder string) *Encoding
        [GoFunc]
        [return: GoReturn("*base32.Encoding")]
        public static Encoding NewEncoding(string encoder)
        {
            return new Encoding(encoder);
        }
    }

    [GoType("struct", Name = "Encoding", Package = "encoding/base32")]
    public class Encoding
    {
        private readonly string _alphabet;
        private long _padding = '=';
        private readonly int[] _decodeMap;

        public Encoding(string alphabet)
        {
            _alphabet = alphabet;
            _decodeMap = new int[256];
            for (int i = 0; i < 256; i++)
            {
                _decodeMap[i] = -1;
            }
            for (int i = 0; i < alphabet.Length && i < 32; i++)
            {
                _decodeMap[(byte)alphabet[i]] = i;
            }
        }

        [GoMethod]
        [return: GoReturn("*base32.Encoding")]
        public Encoding WithPadding(long padding)
        {
            var enc = new Encoding(_alphabet);
            enc._padding = padding;
            return enc;
        }

        [GoMethod]
        public string EncodeToString(Slice<byte> src)
        {
            var bytes = new byte[src.Len];
            for (int i = 0; i < src.Len; i++)
            {
                bytes[i] = src[i];
            }
            return EncodeBytes(bytes);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) DecodeString(string s)
        {
            try
            {
                var decoded = DecodeBytes(s);
                return (new Slice<byte>(decoded), "");
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        [GoMethod]
        public long EncodedLen(long n)
        {
            if (_padding == Package.NoPadding)
            {
                return (n * 8 + 4) / 5;
            }
            return (n + 4) / 5 * 8;
        }

        [GoMethod]
        public long DecodedLen(long n)
        {
            if (_padding == Package.NoPadding)
            {
                return n * 5 / 8;
            }
            return n / 8 * 5;
        }

        [GoMethod]
        public void Encode(Slice<byte> dst, Slice<byte> src)
        {
            var bytes = new byte[src.Len];
            for (int i = 0; i < src.Len; i++)
            {
                bytes[i] = src[i];
            }
            var encoded = EncodeBytes(bytes);
            for (int i = 0; i < encoded.Length && i < dst.Len; i++)
            {
                dst[i] = (byte)encoded[i];
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) Decode(Slice<byte> dst, Slice<byte> src)
        {
            try
            {
                var srcStr = new char[src.Len];
                for (int i = 0; i < src.Len; i++)
                {
                    srcStr[i] = (char)src[i];
                }
                var decoded = DecodeBytes(new string(srcStr));
                int n = System.Math.Min(decoded.Length, dst.Len);
                for (int i = 0; i < n; i++)
                {
                    dst[i] = decoded[i];
                }
                return (n, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*base32.Encoding")]
        public Encoding Strict()
        {
            return this;
        }

        private string EncodeBytes(byte[] data)
        {
            if (data.Length == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            int i = 0;
            while (i < data.Length)
            {
                // Process 5 bytes at a time into 8 base32 characters
                long b0 = (i < data.Length) ? data[i++] : 0;
                long b1 = (i < data.Length) ? data[i++] : 0;
                long b2 = (i < data.Length) ? data[i++] : 0;
                long b3 = (i < data.Length) ? data[i++] : 0;
                long b4 = (i < data.Length) ? data[i++] : 0;

                int remaining = data.Length - (i - 5);
                if (remaining > 5)
                {
                    remaining = 5;
                }

                sb.Append(_alphabet[(int)((b0 >> 3) & 0x1F)]);
                sb.Append(_alphabet[(int)(((b0 << 2) | (b1 >> 6)) & 0x1F)]);

                if (remaining >= 2)
                {
                    sb.Append(_alphabet[(int)((b1 >> 1) & 0x1F)]);
                    sb.Append(_alphabet[(int)(((b1 << 4) | (b2 >> 4)) & 0x1F)]);
                }
                else
                {
                    if (_padding != Package.NoPadding)
                    {
                        sb.Append((char)_padding, 6);
                    }
                    break;
                }

                if (remaining >= 3)
                {
                    sb.Append(_alphabet[(int)(((b2 << 1) | (b3 >> 7)) & 0x1F)]);
                }
                else
                {
                    if (_padding != Package.NoPadding)
                    {
                        sb.Append((char)_padding, 4);
                    }
                    break;
                }

                if (remaining >= 4)
                {
                    sb.Append(_alphabet[(int)((b3 >> 2) & 0x1F)]);
                    sb.Append(_alphabet[(int)(((b3 << 3) | (b4 >> 5)) & 0x1F)]);
                }
                else
                {
                    if (_padding != Package.NoPadding)
                    {
                        sb.Append((char)_padding, 3);
                    }
                    break;
                }

                if (remaining >= 5)
                {
                    sb.Append(_alphabet[(int)(b4 & 0x1F)]);
                }
                else
                {
                    if (_padding != Package.NoPadding)
                    {
                        sb.Append((char)_padding, 1);
                    }
                    break;
                }
            }

            return sb.ToString();
        }

        private byte[] DecodeBytes(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return Array.Empty<byte>();
            }

            // Strip padding
            string s = encoded;
            if (_padding != Package.NoPadding)
            {
                s = s.TrimEnd((char)_padding);
            }

            int outputLen = s.Length * 5 / 8;
            var result = new byte[outputLen];
            int resultIdx = 0;

            int buffer = 0;
            int bitsLeft = 0;

            foreach (char c in s)
            {
                int val = _decodeMap[(byte)c];
                if (val < 0)
                {
                    throw new FormatException("encoding/base32: invalid character: " + c);
                }
                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    if (resultIdx < result.Length)
                    {
                        result[resultIdx++] = (byte)((buffer >> bitsLeft) & 0xFF);
                    }
                }
            }

            if (resultIdx < result.Length)
            {
                var trimmed = new byte[resultIdx];
                Array.Copy(result, trimmed, resultIdx);
                return trimmed;
            }

            return result;
        }
    }
}
