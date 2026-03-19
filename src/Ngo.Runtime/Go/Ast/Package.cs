using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Go.Ast
{
    [GoPackage("go/ast")]
    public static class Package
    {
        // ast.Inspect(node Node, f func(Node) bool)
        [GoFunc]
        public static void Inspect(object? node, Func<object?, bool> f) { }

        // ast.Walk(v Visitor, node Node)
        [GoFunc]
        public static void Walk(object? v, object? node) { }

        // ast.Fprint(w io.Writer, fset *token.FileSet, x any, f FieldFilter) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fprint(object? w, object? fset, object? x, object? f) => null;

        // ast.IsExported(name string) bool
        [GoFunc]
        public static bool IsExported(string name) => name.Length > 0 && char.IsUpper(name[0]);

        // ast.SortImports(fset *token.FileSet, f *ast.File)
        [GoFunc]
        public static void SortImports(object? fset, GoFile? f) { }

        // ast.MergePackageFiles(pkg *ast.Package, mode MergeMode) *ast.File
        [GoFunc]
        [return: GoReturn("*ast.File")]
        public static GoFile? MergePackageFiles(object? pkg, long mode) => null;

        // ast.FilterFile(src *ast.File, f Filter) bool
        [GoFunc]
        public static bool FilterFile(GoFile? src, object? f) => false;

        // ast.NewPackage(fset *token.FileSet, files map[string]*ast.File, ...) (*ast.Package, error)
        [GoFunc]
        [return: GoReturn("*ast.Package", "error")]
        public static (object?, object?) NewPackage(object? fset, object? files, object? importer, object? universe)
            => (null, null);
    }

    // ast.Node interface
    [GoType("interface", Name = "Node", Package = "go/ast")]
    public interface IGoNode
    {
        [GoMethod]
        [return: GoReturn("token.Pos")]
        long Pos();

        [GoMethod]
        [return: GoReturn("token.Pos")]
        long End();
    }

    // ast.Expr interface
    [GoType("interface", Name = "Expr", Package = "go/ast")]
    public interface IGoExpr : IGoNode { }

    // ast.Stmt interface
    [GoType("interface", Name = "Stmt", Package = "go/ast")]
    public interface IGoStmt : IGoNode { }

    // ast.Decl interface
    [GoType("interface", Name = "Decl", Package = "go/ast")]
    public interface IGoDecl : IGoNode { }

    // ast.Ident struct
    [GoType("struct", Name = "Ident", Package = "go/ast")]
    public class GoIdent
    {
        [GoField(Name = "NamePos", Type = "token.Pos")]
        public long NamePos;

        [GoField(Name = "Name")]
        public string Name = "";

        [GoField(Name = "Obj", Type = "*ast.Object")]
        public object? Obj;

        [GoMethod]
        public override string ToString() => Name;
    }

    // ast.File struct
    [GoType("struct", Name = "File", Package = "go/ast")]
    public class GoFile
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")]
        public GoCommentGroup? Doc;

        [GoField(Name = "Package", Type = "token.Pos")]
        public long PackagePos;

        [GoField(Name = "Name", Type = "*ast.Ident")]
        public GoIdent? Name;

        [GoField(Name = "Decls", Type = "[]ast.Decl")]
        public Slice<object?> Decls = new Slice<object?>();

        [GoField(Name = "Scope", Type = "*ast.Scope")]
        public GoScope? Scope;

        [GoField(Name = "Imports", Type = "[]*ast.ImportSpec")]
        public Slice<object?> Imports = new Slice<object?>();

        [GoField(Name = "Unresolved", Type = "[]*ast.Ident")]
        public Slice<GoIdent> Unresolved;

        [GoField(Name = "Comments", Type = "[]*ast.CommentGroup")]
        public Slice<object?> Comments = new Slice<object?>();

        [GoField(Name = "GoVersion")]
        public string GoVersion = "";
    }

    // ast.Scope struct
    [GoType("struct", Name = "Scope", Package = "go/ast")]
    public class GoScope
    {
        [GoField(Name = "Outer", Type = "*ast.Scope")] public GoScope? Outer;
        [GoField(Name = "Objects", Type = "map[string]*ast.Object")] public Map<string, GoObject> Objects = new();
        [GoMethod] [return: GoReturn("*ast.Object")] public GoObject? Lookup(string name) => null;
        [GoMethod] public void Insert([GoParam("*ast.Object")] GoObject? obj) { }
    }

    // ast.Spec interface
    [GoType("interface", Name = "Spec", Package = "go/ast")]
    public interface IGoSpec : IGoNode { }

    // ast.Object struct
    [GoType("struct", Name = "Object", Package = "go/ast")]
    public class GoObject
    {
        [GoField(Name = "Kind", Type = "ast.ObjKind")]
        public long Kind;

        [GoField(Name = "Name")]
        public string Name = "";

        [GoField(Name = "Decl")]
        public object? Decl;

        [GoField(Name = "Data")]
        public object? Data;

        [GoField(Name = "Type")]
        public object? Type;
    }

    // ast.CommentGroup struct
    [GoType("struct", Name = "CommentGroup", Package = "go/ast")]
    public class GoCommentGroup
    {
        [GoField(Name = "List", Type = "[]*ast.Comment")]
        public Slice<GoComment> List;

        [GoMethod] public string Text() => "";
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.Comment struct
    [GoType("struct", Name = "Comment", Package = "go/ast")]
    public class GoComment
    {
        [GoField(Name = "Slash", Type = "token.Pos")] public long Slash;
        [GoField(Name = "Text")] public string Text = "";
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Slash;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Slash;
    }

    // ast.GenDecl struct
    [GoType("struct", Name = "GenDecl", Package = "go/ast")]
    public class GoGenDecl
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")] public GoCommentGroup? Doc;
        [GoField(Name = "TokPos", Type = "token.Pos")] public long TokPos;
        [GoField(Name = "Tok", Type = "token.Token")] public long Tok;
        [GoField(Name = "Lparen", Type = "token.Pos")] public long Lparen;
        [GoField(Name = "Specs", Type = "[]ast.Spec")] public Slice<object?> Specs;
        [GoField(Name = "Rparen", Type = "token.Pos")] public long Rparen;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => TokPos;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rparen;
    }

    // ast.ImportSpec struct
    [GoType("struct", Name = "ImportSpec", Package = "go/ast")]
    public class GoImportSpec
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")] public GoCommentGroup? Doc;
        [GoField(Name = "Name", Type = "*ast.Ident")] public GoIdent? Name;
        [GoField(Name = "Path", Type = "*ast.BasicLit")] public GoBasicLit? Path;
        [GoField(Name = "Comment", Type = "*ast.CommentGroup")] public GoCommentGroup? Comment;
        [GoField(Name = "EndPos", Type = "token.Pos")] public long EndPos;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => EndPos;
    }

    // ast.BasicLit struct
    [GoType("struct", Name = "BasicLit", Package = "go/ast")]
    public class GoBasicLit
    {
        [GoField(Name = "ValuePos", Type = "token.Pos")] public long ValuePos;
        [GoField(Name = "Kind", Type = "token.Token")] public long Kind;
        [GoField(Name = "Value")] public string Value = "";
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => ValuePos;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => ValuePos;
    }

    // ast.Visitor interface
    [GoType("interface", Name = "Visitor", Package = "go/ast")]
    public interface IGoVisitor
    {
        [GoMethod]
        [return: GoReturn("ast.Visitor")]
        object? Visit(object? node);
    }
}
