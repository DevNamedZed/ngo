using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Internal.Gcimporter
{
    [GoPackage("go/internal/gcimporter")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*go/types.Package", "error")]
        public static (object?, object?) Import(object? fset, object? packages, string path, string srcDir, object? lookup) => (null, "not supported");

        [GoFunc]
        [return: GoReturn("int")]
        public static long FindPkg(string path, string srcDir) => 0;
    }
}
