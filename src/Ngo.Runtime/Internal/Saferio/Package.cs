using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Saferio
{
    [GoPackage("internal/saferio")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) ReadData([GoParam("io.Reader")] object? r, [GoParam("uint64")] long n)
        {
            if (n > 1 << 30) return (default, "too large");
            var buf = new byte[(int)n];
            return (new Slice<byte>(buf), null);
        }

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) ReadDataAt([GoParam("io.ReaderAt")] object? r, [GoParam("uint64")] long n, long off)
        {
            if (n > 1 << 30) return (default, "too large");
            var buf = new byte[(int)n];
            return (new Slice<byte>(buf), null);
        }

        [GoFunc]
        [return: GoReturn("*T", "error")]
        public static (Slice<object>, object?) SliceCap([GoParam("uint64")] long c)
        {
            if (c > 1 << 30) return (default, "too large");
            return (new Slice<object>(new object[(int)c]), null);
        }

        [GoConst] public const long ChunkLimit = 10 << 20; // 10MB

        [GoFunc]
        [return: GoReturn("int")]
        public static long SliceCapWithSize([GoParam("uint64")] long size, [GoParam("uint64")] long c)
        {
            if (c > (1L << 30) / System.Math.Max(size, 1)) return -1;
            return (long)c;
        }
    }
}
