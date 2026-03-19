using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Zstd
{
    [GoPackage("internal/zstd")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("io.ReadCloser", "error")]
        public static (object?, object?) NewReader([GoParam("io.Reader")] object? r)
            => (null, "zstd: not supported");
    }
}
