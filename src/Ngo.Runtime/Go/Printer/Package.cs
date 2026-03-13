using Ngo.Runtime.Discovery;
using Ngo.Runtime.Go.Token;

namespace Ngo.Runtime.Go.Printer
{
    [GoPackage("go/printer")]
    public static class Package
    {
        // printer.Fprint(output io.Writer, fset *token.FileSet, node any) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fprint(object? output, GoFileSet? fset, object? node) => null;
    }

    // printer.Config struct
    [GoType("struct", Name = "Config", Package = "go/printer")]
    public class GoConfig
    {
        [GoField(Name = "Mode")]
        public long Mode;

        [GoField(Name = "Tabwidth")]
        public long Tabwidth = 8;

        [GoField(Name = "Indent")]
        public long Indent;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fprint(object? output, GoFileSet? fset, object? node) => null;
    }

    // printer.Mode named type
    [GoType("named", Name = "Mode", Package = "go/printer", Underlying = "uint")]
    public struct GoModeType { }
}
