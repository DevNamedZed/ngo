using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Unicode.Utf16
{
    [GoPackage("unicode/utf16")]
    public static class Package
    {
        private const long ReplacementChar = 0xFFFD;
        private const long SurrogateMin = 0xD800;
        private const long Surr1 = 0xD800;
        private const long Surr2 = 0xDC00;
        private const long Surr3 = 0xE000;
        private const long SurrSelf = 0x10000;
        private const long MaxRune = 0x10FFFF;

        // utf16.Decode(s []uint16) []rune
        [GoFunc]
        [return: GoReturn("[]rune")]
        public static Slice<long> Decode(Slice<ushort> s)
        {
            var runes = new List<long>();
            int i = 0;
            while (i < s.Len)
            {
                long r = s[i];
                if (r < Surr1 || r >= Surr3)
                {
                    // Normal character
                    runes.Add(r);
                }
                else if (r >= Surr1 && r < Surr2 && i + 1 < s.Len && s[i + 1] >= Surr2 && s[i + 1] < Surr3)
                {
                    // Valid surrogate pair
                    runes.Add(DecodeRune(r, s[i + 1]));
                    i++;
                }
                else
                {
                    // Invalid surrogate
                    runes.Add(ReplacementChar);
                }
                i++;
            }
            return new Slice<long>(runes.ToArray());
        }

        // utf16.Encode(s []rune) []uint16
        [GoFunc]
        [return: GoReturn("[]uint16")]
        public static Slice<ushort> Encode([GoParam("[]rune")] Slice<long> s)
        {
            var result = new List<ushort>();
            for (int i = 0; i < s.Len; i++)
            {
                long r = s[i];
                if ((r >= 0 && r < Surr1) || (r >= Surr3 && r < SurrSelf))
                {
                    result.Add((ushort)r);
                }
                else if (r >= SurrSelf && r <= MaxRune)
                {
                    var (r1, r2) = EncodeRune(r);
                    result.Add((ushort)r1);
                    result.Add((ushort)r2);
                }
                else
                {
                    result.Add((ushort)ReplacementChar);
                }
            }
            return new Slice<ushort>(result.ToArray());
        }

        // utf16.AppendRune(a []uint16, r rune) []uint16
        [GoFunc]
        [return: GoReturn("[]uint16")]
        public static Slice<ushort> AppendRune(Slice<ushort> a, [GoParam("rune")] long r)
        {
            if ((r >= 0 && r < Surr1) || (r >= Surr3 && r < SurrSelf))
            {
                return Slice<ushort>.Append(a, (ushort)r);
            }
            if (r >= SurrSelf && r <= MaxRune)
            {
                var (r1, r2) = EncodeRune(r);
                return Slice<ushort>.Append(a, (ushort)r1, (ushort)r2);
            }
            return Slice<ushort>.Append(a, (ushort)ReplacementChar);
        }

        // utf16.DecodeRune(r1, r2 rune) rune
        [GoFunc]
        [return: GoReturn("rune")]
        public static long DecodeRune([GoParam("rune")] long r1, [GoParam("rune")] long r2)
        {
            if (r1 >= Surr1 && r1 < Surr2 && r2 >= Surr2 && r2 < Surr3)
            {
                return ((r1 - Surr1) << 10) | (r2 - Surr2) + SurrSelf;
            }
            return ReplacementChar;
        }

        // utf16.EncodeRune(r rune) (r1, r2 rune)
        [GoFunc]
        [return: GoReturn("rune", "rune")]
        public static (long, long) EncodeRune([GoParam("rune")] long r)
        {
            if (r < SurrSelf || r > MaxRune)
            {
                return (ReplacementChar, ReplacementChar);
            }
            r -= SurrSelf;
            long r1 = Surr1 + (r >> 10) & 0x3FF;
            long r2 = Surr2 + r & 0x3FF;
            return (r1, r2);
        }

        // utf16.IsSurrogate(r rune) bool
        [GoFunc]
        public static bool IsSurrogate([GoParam("rune")] long r)
        {
            return r >= SurrogateMin && r < Surr3;
        }
    }
}
