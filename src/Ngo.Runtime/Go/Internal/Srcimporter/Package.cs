using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Internal.Srcimporter
{
    [GoPackage("go/internal/srcimporter")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Importer")]
        public static GoImporter New(object? ctxt, object? fset, object? packages) => new GoImporter();
    }

    [GoType("struct", Name = "Importer", Package = "go/internal/srcimporter")]
    public class GoImporter
    {
        [GoMethod]
        [return: GoReturn("*go/types.Package", "error")]
        public (object?, object?) Import(string path) => (null, "not supported");

        [GoMethod]
        [return: GoReturn("*go/types.Package", "error")]
        public (object?, object?) ImportFrom(string path, string srcDir, long mode) => (null, "not supported");
    }
}
