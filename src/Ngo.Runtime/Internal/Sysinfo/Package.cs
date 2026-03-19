using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Sysinfo
{
    [GoPackage("internal/sysinfo")]
    public static class Package
    {
        [GoFunc]
        public static string CPUName() => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
    }
}
