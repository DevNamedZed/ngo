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

        // Mode constants
        [GoConst(Type = "printer.Mode")] public const long RawFormat = 1;
        [GoConst(Type = "printer.Mode")] public const long TabIndent = 2;
        [GoConst(Type = "printer.Mode")] public const long UseSpaces = 4;
        [GoConst(Type = "printer.Mode")] public const long SourcePos = 8;
    }

    // printer.CommentedNode struct
    [GoType("struct", Name = "CommentedNode", Package = "go/printer")]
    public class GoCommentedNode
    {
        [GoField(Name = "Node")] public object? Node;
        [GoField(Name = "Comments", Type = "[]*ast.CommentGroup")] public Slice<object?> Comments;
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
