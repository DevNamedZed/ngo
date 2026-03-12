using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Subtle
{
    [GoPackage("crypto/subtle")]
    public static class Package
    {
        // subtle.ConstantTimeCompare(x, y []byte) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeCompare(Slice<byte> x, Slice<byte> y) => 0;

        // subtle.ConstantTimeSelect(v, x, y int) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeSelect([GoParam("int")] long v, [GoParam("int")] long x, [GoParam("int")] long y) => 0;

        // subtle.ConstantTimeByteEq(x, y uint8) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeByteEq(byte x, byte y) => 0;

        // subtle.ConstantTimeEq(x, y int32) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeEq(int x, int y) => 0;

        // subtle.ConstantTimeCopy(v int, x, y []byte)
        [GoFunc]
        public static void ConstantTimeCopy([GoParam("int")] long v, Slice<byte> x, Slice<byte> y) { }

        // subtle.ConstantTimeLessOrEq(x, y int) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long ConstantTimeLessOrEq([GoParam("int")] long x, [GoParam("int")] long y) => 0;

        // subtle.XORBytes(dst, x, y []byte) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long XORBytes(Slice<byte> dst, Slice<byte> x, Slice<byte> y) => 0;
    }
}
