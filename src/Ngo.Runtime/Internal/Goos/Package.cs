using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goos
{
    [GoPackage("internal/goos")]
    public static class Package
    {
        [GoConst]
        public static readonly string GOOS = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows) ? "windows" :
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.OSX) ? "darwin" : "linux";

        [GoConst] public static readonly long IsAix = 0;
        [GoConst] public static readonly long IsAndroid = 0;
        [GoConst] public static readonly long IsDarwin = GOOS == "darwin" ? 1 : 0;
        [GoConst] public static readonly long IsDragonfly = 0;
        [GoConst] public static readonly long IsFreebsd = 0;
        [GoConst] public static readonly long IsHurd = 0;
        [GoConst] public static readonly long IsIllumos = 0;
        [GoConst] public static readonly long IsIos = 0;
        [GoConst] public static readonly long IsJs = 0;
        [GoConst] public static readonly long IsLinux = GOOS == "linux" ? 1 : 0;
        [GoConst] public static readonly long IsNacl = 0;
        [GoConst] public static readonly long IsNetbsd = 0;
        [GoConst] public static readonly long IsOpenbsd = 0;
        [GoConst] public static readonly long IsPlan9 = 0;
        [GoConst] public static readonly long IsSolaris = 0;
        [GoConst] public static readonly long IsWasip1 = 0;
        [GoConst] public static readonly long IsWindows = GOOS == "windows" ? 1 : 0;
        [GoConst] public static readonly long IsZos = 0;
    }
}
