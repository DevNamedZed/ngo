using System;
using System.Numerics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math.Bits
{
    /// <summary>
    /// Runtime support for Go's math/bits package.
    /// </summary>
    [GoPackage("math/bits")]
    public static class Package
    {
        [GoConst]
        public const long UintSize = 64;

        // RotateLeft functions
        [GoFunc]
        public static ulong RotateLeft(ulong x, long k) => BitOperations.RotateLeft(x, (int)k);

        [GoFunc]
        public static byte RotateLeft8(byte x, long k)
        {
            var n = (int)(k % 8);
            if (n < 0) n += 8;
            return (byte)((x << n) | (x >> (8 - n)));
        }

        [GoFunc]
        public static ushort RotateLeft16(ushort x, long k)
        {
            var n = (int)(k % 16);
            if (n < 0) n += 16;
            return (ushort)((x << n) | (x >> (16 - n)));
        }

        [GoFunc]
        public static uint RotateLeft32(uint x, long k) => BitOperations.RotateLeft(x, (int)k);

        [GoFunc]
        public static ulong RotateLeft64(ulong x, long k) => BitOperations.RotateLeft(x, (int)k);

        // OnesCount functions
        [GoFunc]
        public static long OnesCount(ulong x) => BitOperations.PopCount(x);

        [GoFunc]
        public static long OnesCount8(byte x) => BitOperations.PopCount(x);

        [GoFunc]
        public static long OnesCount16(ushort x) => BitOperations.PopCount(x);

        [GoFunc]
        public static long OnesCount32(uint x) => BitOperations.PopCount(x);

        [GoFunc]
        public static long OnesCount64(ulong x) => BitOperations.PopCount(x);

        // LeadingZeros functions
        [GoFunc]
        public static long LeadingZeros(ulong x) => BitOperations.LeadingZeroCount(x);

        [GoFunc]
        public static long LeadingZeros8(byte x) => BitOperations.LeadingZeroCount(x) - 24;

        [GoFunc]
        public static long LeadingZeros16(ushort x) => BitOperations.LeadingZeroCount(x) - 16;

        [GoFunc]
        public static long LeadingZeros32(uint x) => BitOperations.LeadingZeroCount(x);

        [GoFunc]
        public static long LeadingZeros64(ulong x) => BitOperations.LeadingZeroCount(x);

        // TrailingZeros functions
        [GoFunc]
        public static long TrailingZeros(ulong x) => BitOperations.TrailingZeroCount(x);

        [GoFunc]
        public static long TrailingZeros8(byte x) => x == 0 ? 8 : BitOperations.TrailingZeroCount(x);

        [GoFunc]
        public static long TrailingZeros16(ushort x) => x == 0 ? 16 : BitOperations.TrailingZeroCount(x);

        [GoFunc]
        public static long TrailingZeros32(uint x) => BitOperations.TrailingZeroCount(x);

        [GoFunc]
        public static long TrailingZeros64(ulong x) => BitOperations.TrailingZeroCount(x);

        // Len functions
        [GoFunc]
        public static long Len(ulong x) => 64 - BitOperations.LeadingZeroCount(x);

        [GoFunc]
        public static long Len8(byte x) => 8 - (BitOperations.LeadingZeroCount(x) - 24);

        [GoFunc]
        public static long Len16(ushort x) => 16 - (BitOperations.LeadingZeroCount(x) - 16);

        [GoFunc]
        public static long Len32(uint x) => 32 - BitOperations.LeadingZeroCount(x);

        [GoFunc]
        public static long Len64(ulong x) => 64 - BitOperations.LeadingZeroCount(x);

        // Reverse functions
        [GoFunc]
        public static ulong Reverse(ulong x) => Reverse64(x);

        [GoFunc]
        public static byte Reverse8(byte x)
        {
            x = (byte)(((x >> 1) & 0x55) | ((x & 0x55) << 1));
            x = (byte)(((x >> 2) & 0x33) | ((x & 0x33) << 2));
            return (byte)((x >> 4) | (x << 4));
        }

        [GoFunc]
        public static ushort Reverse16(ushort x)
        {
            return (ushort)((Reverse8((byte)x) << 8) | Reverse8((byte)(x >> 8)));
        }

        [GoFunc]
        public static uint Reverse32(uint x)
        {
            x = ((x >> 1) & 0x55555555) | ((x & 0x55555555) << 1);
            x = ((x >> 2) & 0x33333333) | ((x & 0x33333333) << 2);
            x = ((x >> 4) & 0x0F0F0F0F) | ((x & 0x0F0F0F0F) << 4);
            return ReverseBytes32(x);
        }

        [GoFunc]
        public static ulong Reverse64(ulong x)
        {
            x = ((x >> 1) & 0x5555555555555555) | ((x & 0x5555555555555555) << 1);
            x = ((x >> 2) & 0x3333333333333333) | ((x & 0x3333333333333333) << 2);
            x = ((x >> 4) & 0x0F0F0F0F0F0F0F0F) | ((x & 0x0F0F0F0F0F0F0F0F) << 4);
            return ReverseBytes64(x);
        }

        // ReverseBytes functions
        [GoFunc]
        public static ushort ReverseBytes16(ushort x) =>
            (ushort)((x >> 8) | (x << 8));

        [GoFunc]
        public static uint ReverseBytes32(uint x)
        {
            x = ((x >> 8) & 0x00FF00FF) | ((x & 0x00FF00FF) << 8);
            return (x >> 16) | (x << 16);
        }

        [GoFunc]
        public static ulong ReverseBytes64(ulong x)
        {
            x = ((x >> 8) & 0x00FF00FF00FF00FF) | ((x & 0x00FF00FF00FF00FF) << 8);
            x = ((x >> 16) & 0x0000FFFF0000FFFF) | ((x & 0x0000FFFF0000FFFF) << 16);
            return (x >> 32) | (x << 32);
        }

        // Arithmetic: Add
        [GoFunc]
        [return: GoReturn("uint", "uint")]
        public static (ulong, ulong) Add(ulong x, ulong y, ulong carry)
        {
            var sum = x + y + carry;
            var carryOut = ((x & y) | ((x | y) & ~sum)) >> 63;
            return (sum, carryOut);
        }

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (uint, uint) Add32(uint x, uint y, uint carry)
        {
            var sum = (ulong)x + (ulong)y + (ulong)carry;
            return ((uint)sum, (uint)(sum >> 32));
        }

        [GoFunc]
        [return: GoReturn("uint64", "uint64")]
        public static (ulong, ulong) Add64(ulong x, ulong y, ulong carry)
        {
            var sum = x + y + carry;
            var carryOut = ((x & y) | ((x | y) & ~sum)) >> 63;
            return (sum, carryOut);
        }

        // Arithmetic: Sub
        [GoFunc]
        [return: GoReturn("uint", "uint")]
        public static (ulong, ulong) Sub(ulong x, ulong y, ulong borrow)
        {
            var diff = x - y - borrow;
            var borrowOut = ((~x & y) | (~(x ^ y) & diff)) >> 63;
            return (diff, borrowOut);
        }

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (uint, uint) Sub32(uint x, uint y, uint borrow)
        {
            var diff = (ulong)x - (ulong)y - (ulong)borrow;
            return ((uint)diff, (uint)(diff >> 63));
        }

        [GoFunc]
        [return: GoReturn("uint64", "uint64")]
        public static (ulong, ulong) Sub64(ulong x, ulong y, ulong borrow)
        {
            var diff = x - y - borrow;
            var borrowOut = ((~x & y) | (~(x ^ y) & diff)) >> 63;
            return (diff, borrowOut);
        }

        // Arithmetic: Mul
        [GoFunc]
        [return: GoReturn("uint", "uint")]
        public static (ulong, ulong) Mul(ulong x, ulong y)
        {
            return Mul64(x, y);
        }

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (uint, uint) Mul32(uint x, uint y)
        {
            var result = (ulong)x * (ulong)y;
            return ((uint)(result >> 32), (uint)result);
        }

        [GoFunc]
        [return: GoReturn("uint64", "uint64")]
        public static (ulong, ulong) Mul64(ulong x, ulong y)
        {
            var hi = System.Math.BigMul(x, y, out var lo);
            return (hi, lo);
        }

        // Arithmetic: Div
        [GoFunc]
        [return: GoReturn("uint", "uint")]
        public static (ulong, ulong) Div(ulong hi, ulong lo, ulong y)
        {
            return Div64(hi, lo, y);
        }

        [GoFunc]
        [return: GoReturn("uint32", "uint32")]
        public static (uint, uint) Div32(uint hi, uint lo, uint y)
        {
            var n = ((ulong)hi << 32) | lo;
            return ((uint)(n / y), (uint)(n % y));
        }

        [GoFunc]
        [return: GoReturn("uint64", "uint64")]
        public static (ulong, ulong) Div64(ulong hi, ulong lo, ulong y)
        {
            if (y == 0) throw new DivideByZeroException("runtime error: integer divide by zero");
            if (hi == 0) return (lo / y, lo % y);
            // Use 128-bit division via BigMul approach
            var n = (new UInt128(hi, lo));
            var d = (UInt128)y;
            return ((ulong)(n / d), (ulong)(n % d));
        }

        // Arithmetic: Rem
        [GoFunc]
        public static ulong Rem(ulong hi, ulong lo, ulong y)
        {
            var (_, r) = Div(hi, lo, y);
            return r;
        }

        [GoFunc]
        public static uint Rem32(uint hi, uint lo, uint y)
        {
            var (_, r) = Div32(hi, lo, y);
            return r;
        }

        [GoFunc]
        public static ulong Rem64(ulong hi, ulong lo, ulong y)
        {
            var (_, r) = Div64(hi, lo, y);
            return r;
        }
    }
}
