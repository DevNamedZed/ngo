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

    // ast.FieldList struct
    [GoType("struct", Name = "FieldList", Package = "go/ast")]
    public class GoFieldList
    {
        [GoField(Name = "List", Type = "[]*ast.Field")]
        public Slice<object?> List = new Slice<object?>();

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
        [GoMethod] public long NumFields() => List.Len;
    }

    // ast.BlockStmt struct
    [GoType("struct", Name = "BlockStmt", Package = "go/ast")]
    public class GoBlockStmt
    {
        [GoField(Name = "Lbrace", Type = "token.Pos")]
        public long Lbrace;

        [GoField(Name = "List", Type = "[]ast.Stmt")]
        public Slice<object?> List = new Slice<object?>();

        [GoField(Name = "Rbrace", Type = "token.Pos")]
        public long Rbrace;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Lbrace;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rbrace;
    }

    // ast.FuncDecl struct
    [GoType("struct", Name = "FuncDecl", Package = "go/ast")]
    public class GoFuncDecl
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")]
        public object? Doc;

        [GoField(Name = "Recv", Type = "*ast.FieldList")]
        public object? Recv;

        [GoField(Name = "Name", Type = "*ast.Ident")]
        public object? Name;

        [GoField(Name = "Type", Type = "*ast.FuncType")]
        public object? Type;

        [GoField(Name = "Body", Type = "*ast.BlockStmt")]
        public object? Body;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.Field struct
    [GoType("struct", Name = "Field", Package = "go/ast")]
    public class GoField
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")]
        public object? Doc;

        [GoField(Name = "Names", Type = "[]*ast.Ident")]
        public Slice<object?> Names = new Slice<object?>();

        [GoField(Name = "Type", Type = "ast.Expr")]
        public object? Type;

        [GoField(Name = "Tag", Type = "*ast.BasicLit")]
        public object? Tag;

        [GoField(Name = "Comment", Type = "*ast.CommentGroup")]
        public object? Comment;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.InterfaceType struct
    [GoType("struct", Name = "InterfaceType", Package = "go/ast")]
    public class GoInterfaceType
    {
        [GoField(Name = "Methods", Type = "*ast.FieldList")]
        public object? Methods;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.SelectorExpr struct
    [GoType("struct", Name = "SelectorExpr", Package = "go/ast")]
    public class GoSelectorExpr
    {
        [GoField(Name = "X", Type = "ast.Expr")]
        public object? X;

        [GoField(Name = "Sel", Type = "*ast.Ident")]
        public object? Sel;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.StarExpr struct
    [GoType("struct", Name = "StarExpr", Package = "go/ast")]
    public class GoStarExpr
    {
        [GoField(Name = "X", Type = "ast.Expr")]
        public object? X;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.Ellipsis struct
    [GoType("struct", Name = "Ellipsis", Package = "go/ast")]
    public class GoEllipsis
    {
        [GoField(Name = "Ellipsis", Type = "token.Pos")]
        public long EllipsisPos;

        [GoField(Name = "Elt", Type = "ast.Expr")]
        public object? Elt;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => EllipsisPos;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.ArrayType struct
    [GoType("struct", Name = "ArrayType", Package = "go/ast")]
    public class GoArrayType
    {
        [GoField(Name = "Len", Type = "ast.Expr")]
        public object? Len;

        [GoField(Name = "Elt", Type = "ast.Expr")]
        public object? Elt;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.FuncType struct
    [GoType("struct", Name = "FuncType", Package = "go/ast")]
    public class GoFuncType
    {
        [GoField(Name = "Params", Type = "*ast.FieldList")]
        public object? Params;

        [GoField(Name = "Results", Type = "*ast.FieldList")]
        public object? Results;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.MapType struct
    [GoType("struct", Name = "MapType", Package = "go/ast")]
    public class GoMapType
    {
        [GoField(Name = "Key", Type = "ast.Expr")]
        public object? Key;

        [GoField(Name = "Value", Type = "ast.Expr")]
        public object? Value;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "ExprStmt", Package = "go/ast")]
    public class GoExprStmt
    {
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "IfStmt", Package = "go/ast")]
    public class GoIfStmt
    {
        [GoField(Name = "If", Type = "token.Pos")] public long If;
        [GoField(Name = "Init", Type = "ast.Stmt")] public object? Init;
        [GoField(Name = "Cond", Type = "ast.Expr")] public object? Cond;
        [GoField(Name = "Body", Type = "*ast.BlockStmt")] public object? Body;
        [GoField(Name = "Else", Type = "ast.Stmt")] public object? Else;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => If;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "SwitchStmt", Package = "go/ast")]
    public class GoSwitchStmt
    {
        [GoField(Name = "Switch", Type = "token.Pos")] public long Switch;
        [GoField(Name = "Init", Type = "ast.Stmt")] public object? Init;
        [GoField(Name = "Tag", Type = "ast.Expr")] public object? Tag;
        [GoField(Name = "Body", Type = "*ast.BlockStmt")] public object? Body;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Switch;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "TypeSwitchStmt", Package = "go/ast")]
    public class GoTypeSwitchStmt
    {
        [GoField(Name = "Switch", Type = "token.Pos")] public long Switch;
        [GoField(Name = "Init", Type = "ast.Stmt")] public object? Init;
        [GoField(Name = "Assign", Type = "ast.Stmt")] public object? Assign;
        [GoField(Name = "Body", Type = "*ast.BlockStmt")] public object? Body;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Switch;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "CaseClause", Package = "go/ast")]
    public class GoCaseClause
    {
        [GoField(Name = "Case", Type = "token.Pos")] public long Case;
        [GoField(Name = "List", Type = "[]ast.Expr")] public Slice<object?> List = new Slice<object?>();
        [GoField(Name = "Colon", Type = "token.Pos")] public long Colon;
        [GoField(Name = "Body", Type = "[]ast.Stmt")] public Slice<object?> Body = new Slice<object?>();
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Case;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "CommClause", Package = "go/ast")]
    public class GoCommClause
    {
        [GoField(Name = "Case", Type = "token.Pos")] public long Case;
        [GoField(Name = "Comm", Type = "ast.Stmt")] public object? Comm;
        [GoField(Name = "Colon", Type = "token.Pos")] public long Colon;
        [GoField(Name = "Body", Type = "[]ast.Stmt")] public Slice<object?> Body = new Slice<object?>();
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Case;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "AssignStmt", Package = "go/ast")]
    public class GoAssignStmt
    {
        [GoField(Name = "Lhs", Type = "[]ast.Expr")] public Slice<object?> Lhs = new Slice<object?>();
        [GoField(Name = "TokPos", Type = "token.Pos")] public long TokPos;
        [GoField(Name = "Tok", Type = "token.Token")] public long Tok;
        [GoField(Name = "Rhs", Type = "[]ast.Expr")] public Slice<object?> Rhs = new Slice<object?>();
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "ReturnStmt", Package = "go/ast")]
    public class GoReturnStmt
    {
        [GoField(Name = "Return", Type = "token.Pos")] public long Return;
        [GoField(Name = "Results", Type = "[]ast.Expr")] public Slice<object?> Results = new Slice<object?>();
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Return;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "BranchStmt", Package = "go/ast")]
    public class GoBranchStmt
    {
        [GoField(Name = "TokPos", Type = "token.Pos")] public long TokPos;
        [GoField(Name = "Tok", Type = "token.Token")] public long Tok;
        [GoField(Name = "Label", Type = "*ast.Ident")] public object? Label;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => TokPos;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "ForStmt", Package = "go/ast")]
    public class GoForStmt
    {
        [GoField(Name = "For", Type = "token.Pos")] public long For;
        [GoField(Name = "Init", Type = "ast.Stmt")] public object? Init;
        [GoField(Name = "Cond", Type = "ast.Expr")] public object? Cond;
        [GoField(Name = "Post", Type = "ast.Stmt")] public object? Post;
        [GoField(Name = "Body", Type = "*ast.BlockStmt")] public object? Body;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => For;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "RangeStmt", Package = "go/ast")]
    public class GoRangeStmt
    {
        [GoField(Name = "For", Type = "token.Pos")] public long For;
        [GoField(Name = "Key", Type = "ast.Expr")] public object? Key;
        [GoField(Name = "Value", Type = "ast.Expr")] public object? Value;
        [GoField(Name = "TokPos", Type = "token.Pos")] public long TokPos;
        [GoField(Name = "Tok", Type = "token.Token")] public long Tok;
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoField(Name = "Body", Type = "*ast.BlockStmt")] public object? Body;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => For;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "TypeSpec", Package = "go/ast")]
    public class GoTypeSpec
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")] public object? Doc;
        [GoField(Name = "Name", Type = "*ast.Ident")] public object? Name;
        [GoField(Name = "TypeParams", Type = "*ast.FieldList")] public object? TypeParams;
        [GoField(Name = "Assign", Type = "token.Pos")] public long Assign;
        [GoField(Name = "Type", Type = "ast.Expr")] public object? Type;
        [GoField(Name = "Comment", Type = "*ast.CommentGroup")] public object? Comment;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "ValueSpec", Package = "go/ast")]
    public class GoValueSpec
    {
        [GoField(Name = "Doc", Type = "*ast.CommentGroup")] public object? Doc;
        [GoField(Name = "Names", Type = "[]*ast.Ident")] public Slice<object?> Names = new Slice<object?>();
        [GoField(Name = "Type", Type = "ast.Expr")] public object? Type;
        [GoField(Name = "Values", Type = "[]ast.Expr")] public Slice<object?> Values = new Slice<object?>();
        [GoField(Name = "Comment", Type = "*ast.CommentGroup")] public object? Comment;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "CallExpr", Package = "go/ast")]
    public class GoCallExpr
    {
        [GoField(Name = "Fun", Type = "ast.Expr")] public object? Fun;
        [GoField(Name = "Lparen", Type = "token.Pos")] public long Lparen;
        [GoField(Name = "Args", Type = "[]ast.Expr")] public Slice<object?> Args = new Slice<object?>();
        [GoField(Name = "Ellipsis", Type = "token.Pos")] public long Ellipsis;
        [GoField(Name = "Rparen", Type = "token.Pos")] public long Rparen;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rparen;
    }

    [GoType("struct", Name = "BinaryExpr", Package = "go/ast")]
    public class GoBinaryExpr
    {
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoField(Name = "OpPos", Type = "token.Pos")] public long OpPos;
        [GoField(Name = "Op", Type = "token.Token")] public long Op;
        [GoField(Name = "Y", Type = "ast.Expr")] public object? Y;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "UnaryExpr", Package = "go/ast")]
    public class GoUnaryExpr
    {
        [GoField(Name = "OpPos", Type = "token.Pos")] public long OpPos;
        [GoField(Name = "Op", Type = "token.Token")] public long Op;
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => OpPos;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "ParenExpr", Package = "go/ast")]
    public class GoParenExpr
    {
        [GoField(Name = "Lparen", Type = "token.Pos")] public long Lparen;
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoField(Name = "Rparen", Type = "token.Pos")] public long Rparen;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Lparen;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rparen;
    }

    [GoType("struct", Name = "IndexExpr", Package = "go/ast")]
    public class GoIndexExpr
    {
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoField(Name = "Lbrack", Type = "token.Pos")] public long Lbrack;
        [GoField(Name = "Index", Type = "ast.Expr")] public object? Index;
        [GoField(Name = "Rbrack", Type = "token.Pos")] public long Rbrack;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rbrack;
    }

    [GoType("struct", Name = "KeyValueExpr", Package = "go/ast")]
    public class GoKeyValueExpr
    {
        [GoField(Name = "Key", Type = "ast.Expr")] public object? Key;
        [GoField(Name = "Colon", Type = "token.Pos")] public long Colon;
        [GoField(Name = "Value", Type = "ast.Expr")] public object? Value;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    [GoType("struct", Name = "CompositeLit", Package = "go/ast")]
    public class GoCompositeLit
    {
        [GoField(Name = "Type", Type = "ast.Expr")] public object? Type;
        [GoField(Name = "Lbrace", Type = "token.Pos")] public long Lbrace;
        [GoField(Name = "Elts", Type = "[]ast.Expr")] public Slice<object?> Elts = new Slice<object?>();
        [GoField(Name = "Rbrace", Type = "token.Pos")] public long Rbrace;
        [GoField(Name = "Incomplete")] public bool Incomplete;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rbrace;
    }

    [GoType("struct", Name = "TypeAssertExpr", Package = "go/ast")]
    public class GoTypeAssertExpr
    {
        [GoField(Name = "X", Type = "ast.Expr")] public object? X;
        [GoField(Name = "Lparen", Type = "token.Pos")] public long Lparen;
        [GoField(Name = "Type", Type = "ast.Expr")] public object? Type;
        [GoField(Name = "Rparen", Type = "token.Pos")] public long Rparen;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => Rparen;
    }

    [GoType("struct", Name = "StructType", Package = "go/ast")]
    public class GoStructType
    {
        [GoField(Name = "Struct", Type = "token.Pos")] public long Struct;
        [GoField(Name = "Fields", Type = "*ast.FieldList")] public object? Fields;
        [GoField(Name = "Incomplete")] public bool Incomplete;
        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => Struct;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }

    // ast.ChanDir named type
    [GoType("named", Name = "ChanDir", Package = "go/ast", Underlying = "int")]
    public class GoChanDir
    {
        public long Value;
    }

    // ast.ChanType struct
    [GoType("struct", Name = "ChanType", Package = "go/ast")]
    public class GoChanType
    {
        [GoField(Name = "Dir", Type = "ast.ChanDir")]
        public long Dir;

        [GoField(Name = "Value", Type = "ast.Expr")]
        public object? Value;

        [GoMethod] [return: GoReturn("token.Pos")] public long Pos() => 0;
        [GoMethod] [return: GoReturn("token.Pos")] public long End() => 0;
    }
}
