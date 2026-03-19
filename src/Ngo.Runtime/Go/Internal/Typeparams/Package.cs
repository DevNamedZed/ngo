using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Internal.Typeparams
{
    /// <summary>
    /// Stub for go/internal/typeparams — type parameter utilities for go/parser and go/types.
    /// </summary>
    [GoPackage("go/internal/typeparams")]
    public static class Package
    {
        // func UnpackIndexExpr(n ast.Node) (x ast.Expr, lbrack token.Pos, indices []ast.Expr, rbrack token.Pos)
        // Returns the components of an index expression (for generic instantiation syntax)
        [GoFunc]
        [return: GoReturn("go/ast.Expr", "go/token.Pos", "[]go/ast.Expr", "go/token.Pos")]
        public static (object?, long, Slice<object?>, long) UnpackIndexExpr(object? n)
            => (null, 0, default, 0);

        // func PackIndexExpr(x ast.Expr, lbrack token.Pos, indices []ast.Expr, rbrack token.Pos) ast.Expr
        [GoFunc]
        [return: GoReturn("go/ast.Expr")]
        public static object? PackIndexExpr(object? x, long lbrack, Slice<object?> indices, long rbrack)
            => x;

        // func IsListExpr(n ast.Node) bool
        [GoFunc]
        public static bool IsListExpr(object? n) => false;
    }

    // IndexExpr wraps an ast.IndexExpr or ast.IndexListExpr
    [GoType("struct", Name = "IndexExpr", Package = "go/internal/typeparams")]
    public class GoIndexExpr
    {
        [GoField(Name = "X", Type = "go/ast.Expr")] public object? X;
        [GoField(Name = "Lbrack", Type = "go/token.Pos")] public long Lbrack;
        [GoField(Name = "Indices", Type = "[]go/ast.Expr")] public Slice<object?> Indices;
        [GoField(Name = "Rbrack", Type = "go/token.Pos")] public long Rbrack;

        [GoMethod] [return: GoReturn("go/token.Pos")] public long Pos() => Lbrack;
        [GoMethod] [return: GoReturn("go/token.Pos")] public long End() => Rbrack;
    }
}
