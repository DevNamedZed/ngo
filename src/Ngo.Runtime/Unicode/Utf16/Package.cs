using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Unicode.Utf16
{
    [GoPackage("unicode/utf16")]
    public static class Package
    {
        // utf16.Decode(s []uint16) []rune
        [GoFunc]
        [return: GoReturn("[]rune")]
        public static Slice<long> Decode(Slice<ushort> s)
        {
            var result = new long[s.Len];
            for (int i = 0; i < s.Len; i++)
                result[i] = s[i];
            return new Slice<long>(result);
        }

        // utf16.Encode(s []rune) []uint16
        [GoFunc]
        [return: GoReturn("[]uint16")]
        public static Slice<ushort> Encode([GoParam("[]rune")] Slice<long> s)
        {
            var result = new ushort[s.Len];
            for (int i = 0; i < s.Len; i++)
                result[i] = (ushort)s[i];
            return new Slice<ushort>(result);
        }

        // utf16.DecodeRune(r1, r2 rune) rune
        [GoFunc]
        [return: GoReturn("rune")]
        public static long DecodeRune([GoParam("rune")] long r1, [GoParam("rune")] long r2)
        {
            return ((r1 - 0xD800) << 10) | (r2 - 0xDC00) + 0x10000;
        }

        // utf16.EncodeRune(r rune) (r1, r2 rune)
        [GoFunc]
        [return: GoReturn("rune", "rune")]
        public static (long, long) EncodeRune([GoParam("rune")] long r)
        {
            long r1 = (r >> 10) + 0xD800;
            long r2 = (r & 0x3FF) + 0xDC00;
            return (r1, r2);
        }

        // utf16.IsSurrogate(r rune) bool
        [GoFunc]
        public static bool IsSurrogate([GoParam("rune")] long r)
        {
            return r >= 0xD800 && r < 0xE000;
        }
    }
}
