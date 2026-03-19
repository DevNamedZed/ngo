using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goversion
{
    /// <summary>
    /// Stub for internal/goversion — Go version constants.
    /// </summary>
    [GoPackage("internal/goversion")]
    public static class Package
    {
        // const Version = 22 (for Go 1.22)
        [GoConst]
        public const long Version = 22;
    }
}
