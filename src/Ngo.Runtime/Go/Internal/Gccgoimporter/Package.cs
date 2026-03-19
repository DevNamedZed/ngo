using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Internal.Gccgoimporter
{
    [GoPackage("go/internal/gccgoimporter")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("go/types.Importer")]
        public static object? GetImporter(Slice<string> searchpaths, object? initmap) => null;
    }

    [GoType("struct", Name = "Importer", Package = "go/internal/gccgoimporter")]
    public class GoImporter
    {
        [GoMethod]
        [return: GoReturn("*go/types.Package", "error")]
        public (object?, object?) Import(string path) => (null, "not supported");
    }

    [GoType("struct", Name = "GccgoInstallation", Package = "go/internal/gccgoimporter")]
    public class GoGccgoInstallation
    {
        [GoField(Name = "GccVersion")] public string GccVersion = "";
        [GoField(Name = "LibDir")] public string LibDir = "";

        [GoMethod]
        [return: GoReturn("go/types.Importer", "error")]
        public (object?, object?) GetImporter(Slice<string> incpaths, object? initmap)
            => (null, "not supported");

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) SearchPaths()
            => (default, null);

        [GoMethod(IsVariadic = true)]
        [return: GoReturn("error")]
        public object? InitFromDriver(string gccgoPath, params string[] args) => null;
    }
}
