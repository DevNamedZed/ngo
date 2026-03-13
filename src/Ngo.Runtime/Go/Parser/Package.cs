using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Parser
{
    [GoPackage("go/parser")]
    public static class Package
    {
        // parser.ParseFile(fset *token.FileSet, filename string, src any, mode Mode) (*ast.File, error)
        [GoFunc]
        [return: GoReturn("*ast.File", "error")]
        public static (Ngo.Runtime.Go.Ast.GoFile?, object?) ParseFile(Ngo.Runtime.Go.Token.GoFileSet? fset, string filename, object? src, long mode)
            => (new Ngo.Runtime.Go.Ast.GoFile { Name = new Ngo.Runtime.Go.Ast.GoIdent { Name = "main" } }, null);

        // parser.ParseExpr(x string) (ast.Expr, error)
        [GoFunc]
        [return: GoReturn("ast.Expr", "error")]
        public static (object?, object?) ParseExpr(string x) => (null, null);

        // Mode constants
        [GoConst]
        public const long PackageClauseOnly = 1;

        [GoConst]
        public const long ImportsOnly = 2;

        [GoConst]
        public const long ParseComments = 4;

        [GoConst]
        public const long Trace = 8;

        [GoConst]
        public const long DeclarationErrors = 16;

        [GoConst]
        public const long AllErrors = 32;
    }

    // parser.Mode named type
    [GoType("named", Name = "Mode", Package = "go/parser", Underlying = "uint")]
    public struct GoModeType { }
}
