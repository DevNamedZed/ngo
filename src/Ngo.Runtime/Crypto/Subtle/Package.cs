using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Subtle
{
    [GoPackage("crypto/subtle")]
    public static class Package
    {
        // subtle.ConstantTimeCompare(x, y []byte) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeCompare(Slice<byte> x, Slice<byte> y)
        {
            if (x.Len != y.Len)
            {
                return 0;
            }
            if (x.Len == 0)
            {
                return 1;
            }
            var xArr = new byte[x.Len];
            var yArr = new byte[y.Len];
            for (int i = 0; i < x.Len; i++)
            {
                xArr[i] = x[i];
            }
            for (int i = 0; i < y.Len; i++)
            {
                yArr[i] = y[i];
            }
            return CryptographicOperations.FixedTimeEquals(xArr, yArr) ? 1 : 0;
        }

        // subtle.ConstantTimeSelect(v, x, y int) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeSelect([GoParam("int")] long v, [GoParam("int")] long x, [GoParam("int")] long y)
        {
            return (~(v - 1) & x) | ((v - 1) & y);
        }

        // subtle.ConstantTimeByteEq(x, y uint8) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeByteEq(byte x, byte y)
        {
            return ConstantTimeEq(x, y);
        }

        // subtle.ConstantTimeEq(x, y int32) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeEq(int x, int y)
        {
            uint z = (uint)(x ^ y);
            return (long)(1 & ((z | (~z + 1)) >> 31) ^ 1);
        }

        // subtle.ConstantTimeCopy(v int, x, y []byte)
        [GoFunc]
        public static void ConstantTimeCopy([GoParam("int")] long v, Slice<byte> x, Slice<byte> y)
        {
            if (x.Len != y.Len)
            {
                throw new GoPanicException("subtle: slices have different lengths");
            }
            long xmask = ~(v - 1);
            long ymask = v - 1;
            for (int i = 0; i < x.Len; i++)
            {
                x[i] = (byte)((x[i] & xmask) | (y[i] & ymask));
            }
        }

        // subtle.ConstantTimeLessOrEq(x, y int) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeLessOrEq([GoParam("int")] long x, [GoParam("int")] long y)
        {
            int x32 = (int)x;
            int y32 = (int)y;
            return (long)(((x32 - y32 - 1) >> 31) & 1);
        }

        // subtle.XORBytes(dst, x, y []byte) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long XORBytes(Slice<byte> dst, Slice<byte> x, Slice<byte> y)
        {
            int n = System.Math.Min(x.Len, y.Len);
            if (dst.Len < n)
            {
                n = dst.Len;
            }
            for (int i = 0; i < n; i++)
            {
                dst[i] = (byte)(x[i] ^ y[i]);
            }
            return n;
        }
    }
}
