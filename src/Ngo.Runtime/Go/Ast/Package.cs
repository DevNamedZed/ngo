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
        [GoField(Name = "Package", Type = "token.Pos")]
        public long PackagePos;

        [GoField(Name = "Name", Type = "*ast.Ident")]
        public GoIdent? Name;

        [GoField(Name = "Decls", Type = "[]ast.Decl")]
        public Slice<object?> Decls = new Slice<object?>();

        [GoField(Name = "Imports", Type = "[]*ast.ImportSpec")]
        public Slice<object?> Imports = new Slice<object?>();

        [GoField(Name = "Comments", Type = "[]*ast.CommentGroup")]
        public Slice<object?> Comments = new Slice<object?>();
    }

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

    // ast.Visitor interface
    [GoType("interface", Name = "Visitor", Package = "go/ast")]
    public interface IGoVisitor
    {
        [GoMethod]
        [return: GoReturn("ast.Visitor")]
        object? Visit(object? node);
    }
}
