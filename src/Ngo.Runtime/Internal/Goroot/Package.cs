using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goroot
{
    /// <summary>
    /// Stub for internal/goroot — Go root directory utilities.
    /// </summary>
    [GoPackage("internal/goroot")]
    public static class Package
    {
        // func IsStandardPackage(goroot, compiler, path string) bool
        [GoFunc]
        public static bool IsStandardPackage(string goroot, string compiler, string path)
        {
            // All stdlib paths are standard
            return !path.Contains(".");
        }
    }
}
