using System.Globalization;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Utf8
{
    /// <summary>
    /// Runtime support for Go's unicode/utf8 package.
    /// </summary>
    [GoPackage("unicode/utf8")]
    public static class Package
    {
        // utf8.RuneCountInString(s string) int
        public static long RuneCountInString(string s)
        {
            if (s == null) return 0;
            var info = new StringInfo(s);
            return info.LengthInTextElements;
        }

        // utf8.ValidString(s string) bool
        public static bool ValidString(string s)
        {
            // .NET strings are always valid UTF-16, but check for unpaired surrogates
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]))
                {
                    if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
                        return false;
                    i++;
                }
                else if (char.IsLowSurrogate(s[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // utf8.DecodeRuneInString(s string) (rune, size int)
        public static (long, long) DecodeRuneInString(string s)
        {
            if (string.IsNullOrEmpty(s)) return (0xFFFD, 0); // RuneError
            char c = s[0];
            if (char.IsHighSurrogate(c) && s.Length > 1 && char.IsLowSurrogate(s[1]))
            {
                int codePoint = char.ConvertToUtf32(c, s[1]);
                return (codePoint, 4);
            }
            // Single UTF-16 char -> 1-3 bytes in UTF-8
            int byteLen = c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            return (c, byteLen);
        }

        // utf8.RuneLen(r rune) int
        public static long RuneLen(long r)
        {
            if (r < 0) return -1;
            if (r < 0x80) return 1;
            if (r < 0x800) return 2;
            if (r < 0x10000) return 3;
            if (r < 0x110000) return 4;
            return -1;
        }

        // utf8.FullRune(p []byte) bool
        public static bool FullRune(Slice<byte> p)
        {
            if (p.Len == 0) return false;
            byte b = p[0];
            if (b < 0x80) return true;
            if (p.Len >= 2 && b < 0xE0) return true;
            if (p.Len >= 3 && b < 0xF0) return true;
            if (p.Len >= 4) return true;
            return false;
        }

        // utf8.EncodeRune(p []byte, r rune) int
        public static long EncodeRune(Slice<byte> p, long r)
        {
            uint rune = (uint)r;
            if (rune < 0x80)
            {
                p[0] = (byte)rune;
                return 1;
            }
            if (rune < 0x800)
            {
                p[0] = (byte)(0xC0 | (rune >> 6));
                p[1] = (byte)(0x80 | (rune & 0x3F));
                return 2;
            }
            if (rune < 0x10000)
            {
                p[0] = (byte)(0xE0 | (rune >> 12));
                p[1] = (byte)(0x80 | ((rune >> 6) & 0x3F));
                p[2] = (byte)(0x80 | (rune & 0x3F));
                return 3;
            }
            p[0] = (byte)(0xF0 | (rune >> 18));
            p[1] = (byte)(0x80 | ((rune >> 12) & 0x3F));
            p[2] = (byte)(0x80 | ((rune >> 6) & 0x3F));
            p[3] = (byte)(0x80 | (rune & 0x3F));
            return 4;
        }

        // utf8.DecodeRune(p []byte) (rune, size int)
        public static (long, long) DecodeRune(Slice<byte> p)
        {
            if (p.Len == 0) return (0xFFFD, 0);
            byte b0 = p[0];
            if (b0 < 0x80) return (b0, 1);
            if (b0 < 0xC0) return (0xFFFD, 1);
            if (b0 < 0xE0)
            {
                if (p.Len < 2) return (0xFFFD, 1);
                int r2 = ((b0 & 0x1F) << 6) | (p[1] & 0x3F);
                return (r2, 2);
            }
            if (b0 < 0xF0)
            {
                if (p.Len < 3) return (0xFFFD, 1);
                int r3 = ((b0 & 0x0F) << 12) | ((p[1] & 0x3F) << 6) | (p[2] & 0x3F);
                return (r3, 3);
            }
            if (p.Len < 4) return (0xFFFD, 1);
            int r4 = ((b0 & 0x07) << 18) | ((p[1] & 0x3F) << 12) | ((p[2] & 0x3F) << 6) | (p[3] & 0x3F);
            return (r4, 4);
        }

        // utf8.Valid(p []byte) bool
        public static bool Valid(Slice<byte> p)
        {
            for (int i = 0; i < p.Len;)
            {
                var (r, size) = DecodeRune(p.Reslice(i, p.Len));
                if (r == 0xFFFD && size <= 1) return false;
                i += (int)size;
            }
            return true;
        }

        // utf8.RuneCount(p []byte) int
        public static long RuneCount(Slice<byte> p)
        {
            int count = 0;
            for (int i = 0; i < p.Len;)
            {
                var (_, size) = DecodeRune(p.Reslice(i, p.Len));
                i += (int)size;
                count++;
            }
            return count;
        }

        // utf8.DecodeLastRuneInString(s string) (rune, size int)
        public static (long, long) DecodeLastRuneInString(string s)
        {
            if (string.IsNullOrEmpty(s)) return (0xFFFD, 0);
            int i = s.Length - 1;
            char c = s[i];
            if (c < 0x80) return (c, 1);
            // Handle multi-byte: encode to UTF-8 and decode last rune
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            return DecodeLastRune(slice);
        }

        // utf8.DecodeLastRune(p []byte) (rune, size int)
        public static (long, long) DecodeLastRune(Slice<byte> p)
        {
            if (p.Len == 0) return (0xFFFD, 0);
            int last = p.Len - 1;
            byte b = p[last];
            if (b < 0x80) return (b, 1);
            // Walk backwards to find start of multi-byte sequence
            int start = last;
            for (int i = 1; i <= 3 && start > 0; i++)
            {
                start--;
                if ((p[start] & 0xC0) != 0x80) break;
            }
            var sub = p.Reslice(start, p.Len);
            var (r, size) = DecodeRune(sub);
            if (start + (int)size != p.Len) return (0xFFFD, 1);
            return (r, size);
        }

        // utf8.ValidRune(r rune) bool
        public static bool ValidRune(long r)
        {
            if (r < 0) return false;
            if (r >= 0xD800 && r <= 0xDFFF) return false; // surrogates
            if (r > 0x10FFFF) return false;
            return true;
        }

        // utf8.AppendRune(p []byte, r rune) []byte
        public static Slice<byte> AppendRune(Slice<byte> p, long r)
        {
            var buf = new byte[4];
            var bufSlice = new Slice<byte>(buf);
            var size = EncodeRune(bufSlice, r);
            var appendBytes = new byte[(int)size];
            System.Array.Copy(buf, appendBytes, (int)size);
            return Slice<byte>.Append(p, appendBytes);
        }

        // utf8.FullRuneInString(s string) bool
        public static bool FullRuneInString(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            return FullRune(new Slice<byte>(bytes));
        }

        // utf8.RuneStart(b byte) bool
        [GoFunc]
        public static bool RuneStart(byte b)
        {
            return (b & 0xC0) != 0x80;
        }

        // Constants
        [GoConst] public static readonly long RuneError = 0xFFFD;
        [GoConst] public static readonly long MaxRune = 0x10FFFF;
        [GoConst] public static readonly long UTFMax = 4;
        [GoConst] public static readonly long RuneSelf = 0x80;
    }
}
