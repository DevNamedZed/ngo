// -----------------------------------------------------------------------
// <copyright file="ParserTests.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Syntax;

[TestClass]
public class ParserTests
{
    private static SourceFileSyntax Parse(string source)
    {
        var parser = new Parser(source);
        return parser.ParseSourceFile();
    }

    // ================================================================
    // Package clause
    // ================================================================

    [TestMethod]
    public void Package_clause()
    {
        var file = Parse("package main");
        Assert.AreEqual(SyntaxKind.PackageClause, file.PackageClause.Kind);
        Assert.AreEqual("package", file.PackageClause.PackageKeyword.Text);
        Assert.AreEqual("main", file.PackageClause.Name.Text);
    }

    // ================================================================
    // Imports
    // ================================================================

    [TestMethod]
    public void Single_import()
    {
        var file = Parse("package main\nimport \"fmt\"");
        Assert.AreEqual(1, file.Imports.Count);
        Assert.IsNull(file.Imports[0].OpenParen);
        Assert.AreEqual(1, file.Imports[0].Specs.Count);
        Assert.AreEqual("\"fmt\"", file.Imports[0].Specs[0].Path.Text);
    }

    [TestMethod]
    public void Block_import()
    {
        var file = Parse("package main\nimport (\n\"fmt\"\n\"os\"\n)");
        Assert.AreEqual(1, file.Imports.Count);
        var imp = file.Imports[0];
        Assert.IsNotNull(imp.OpenParen);
        Assert.AreEqual(2, imp.Specs.Count);
        Assert.AreEqual("\"fmt\"", imp.Specs[0].Path.Text);
        Assert.AreEqual("\"os\"", imp.Specs[1].Path.Text);
    }

    [TestMethod]
    public void Import_with_alias()
    {
        var file = Parse("package main\nimport f \"fmt\"");
        var spec = file.Imports[0].Specs[0];
        Assert.IsNotNull(spec.Alias);
        Assert.AreEqual("f", spec.Alias!.Text);
        Assert.AreEqual("\"fmt\"", spec.Path.Text);
    }

    [TestMethod]
    public void Import_with_dot()
    {
        var file = Parse("package main\nimport . \"fmt\"");
        var spec = file.Imports[0].Specs[0];
        Assert.IsNotNull(spec.Alias);
        Assert.AreEqual(".", spec.Alias!.Text);
    }

    // ================================================================
    // Function declarations
    // ================================================================

    [TestMethod]
    public void Simple_function()
    {
        var file = Parse("package main\nfunc main() {}");
        Assert.AreEqual(1, file.Members.Count);
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.AreEqual("main", fn.Name.Text);
        Assert.AreEqual(0, fn.Parameters.Parameters.Count);
        Assert.IsNotNull(fn.Body);
    }

    [TestMethod]
    public void Function_with_params_and_return()
    {
        var file = Parse("package main\nfunc add(a int, b int) int { return a + b }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.AreEqual("add", fn.Name.Text);
        Assert.AreEqual(2, fn.Parameters.Parameters.Count);
        Assert.IsNotNull(fn.Result);
    }

    [TestMethod]
    public void Method_declaration()
    {
        var file = Parse("package main\nfunc (s *Server) Start() error { return nil }");
        Assert.IsInstanceOfType<MethodDeclarationSyntax>(file.Members[0]);
        var method = (MethodDeclarationSyntax)file.Members[0];
        Assert.AreEqual("Start", method.Name.Text);
        Assert.AreEqual(1, method.Receiver.Parameters.Count);
    }

    // ================================================================
    // Var/Const declarations
    // ================================================================

    [TestMethod]
    public void Var_with_type()
    {
        var file = Parse("package main\nvar x int");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.AreEqual(1, varDecl.Specs.Count);
        Assert.AreEqual("x", varDecl.Specs[0].Names[0].Text);
    }

    [TestMethod]
    public void Var_with_initializer()
    {
        var file = Parse("package main\nvar x = 42");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.IsNotNull(varDecl.Specs[0].EqualsToken);
        Assert.IsNotNull(varDecl.Specs[0].Values);
    }

    [TestMethod]
    public void Var_block()
    {
        var file = Parse("package main\nvar (\nx int\ny string\n)");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.AreEqual(2, varDecl.Specs.Count);
    }

    [TestMethod]
    public void Const_declaration()
    {
        var file = Parse("package main\nconst pi = 3.14");
        Assert.IsInstanceOfType<ConstDeclarationSyntax>(file.Members[0]);
        var constDecl = (ConstDeclarationSyntax)file.Members[0];
        Assert.AreEqual(1, constDecl.Specs.Count);
        Assert.AreEqual("pi", constDecl.Specs[0].Names[0].Text);
    }

    // ================================================================
    // Type declarations
    // ================================================================

    [TestMethod]
    public void Type_declaration_struct()
    {
        var file = Parse("package main\ntype Point struct {\nX int\nY int\n}");
        Assert.IsInstanceOfType<TypeDeclarationSyntax>(file.Members[0]);
        var typeDecl = (TypeDeclarationSyntax)file.Members[0];
        Assert.AreEqual("Point", typeDecl.Specs[0].Name.Text);
        Assert.IsInstanceOfType<StructTypeSyntax>(typeDecl.Specs[0].Type);
        var structType = (StructTypeSyntax)typeDecl.Specs[0].Type;
        Assert.AreEqual(2, structType.Fields.Count);
    }

    [TestMethod]
    public void Type_alias()
    {
        var file = Parse("package main\ntype MyInt = int");
        Assert.IsInstanceOfType<TypeDeclarationSyntax>(file.Members[0]);
        var typeDecl = (TypeDeclarationSyntax)file.Members[0];
        Assert.IsNotNull(typeDecl.Specs[0].AssignToken);
    }

    // ================================================================
    // Statements
    // ================================================================

    [TestMethod]
    public void Return_statement()
    {
        var file = Parse("package main\nfunc f() int { return 42 }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ReturnStatementSyntax>(fn.Body!.Statements[0]);
        var ret = (ReturnStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(1, ret.Values.Count);
    }

    [TestMethod]
    public void Return_no_value()
    {
        var file = Parse("package main\nfunc f() { return }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ReturnStatementSyntax>(fn.Body!.Statements[0]);
        var ret = (ReturnStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(0, ret.Values.Count);
    }

    [TestMethod]
    public void If_statement()
    {
        var file = Parse("package main\nfunc f() { if x > 0 { return } }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<IfStatementSyntax>(fn.Body!.Statements[0]);
        var ifStmt = (IfStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNull(ifStmt.Init);
        Assert.IsNotNull(ifStmt.Body);
        Assert.IsNull(ifStmt.ElseBody);
    }

    [TestMethod]
    public void If_else()
    {
        var file = Parse("package main\nfunc f() { if x > 0 { return } else { return } }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<IfStatementSyntax>(fn.Body!.Statements[0]);
        var ifStmt = (IfStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(ifStmt.ElseKeyword);
        Assert.IsInstanceOfType<BlockSyntax>(ifStmt.ElseBody);
    }

    [TestMethod]
    public void If_else_if()
    {
        var file = Parse("package main\nfunc f() { if x > 0 { return } else if x < 0 { return } }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<IfStatementSyntax>(fn.Body!.Statements[0]);
        var ifStmt = (IfStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<IfStatementSyntax>(ifStmt.ElseBody);
    }

    [TestMethod]
    public void For_infinite()
    {
        var file = Parse("package main\nfunc f() { for {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNull(forStmt.Condition);
        Assert.IsNull(forStmt.RangeClause);
    }

    [TestMethod]
    public void For_condition()
    {
        var file = Parse("package main\nfunc f() { for x < 10 {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNull(forStmt.Init);
    }

    [TestMethod]
    public void For_clause()
    {
        var file = Parse("package main\nfunc f() { for i := 0; i < 10; i++ {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(forStmt.Init);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Post);
    }

    [TestMethod]
    public void For_range()
    {
        var file = Parse("package main\nfunc f() { for k, v := range items {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(forStmt.RangeClause);
        Assert.AreEqual(2, forStmt.RangeClause!.Variables!.Value.Count);
    }

    [TestMethod]
    public void For_multi_value_short_var_decl()
    {
        var file = Parse("package main\nfunc f() { for i, j := 0, 10; i < j; i++ {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(forStmt.Init);
        Assert.IsInstanceOfType<ShortVarDeclarationSyntax>(forStmt.Init);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Post);
    }

    [TestMethod]
    public void For_multi_value_assignment()
    {
        var file = Parse("package main\nfunc f() { for a, b = x, y; a < b; a++ {} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ForStatementSyntax>(fn.Body!.Statements[0]);
        var forStmt = (ForStatementSyntax)fn.Body!.Statements[0];
        Assert.IsNotNull(forStmt.Init);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(forStmt.Init);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Post);
    }

    [TestMethod]
    public void Nested_composite_literal_bare()
    {
        var file = Parse("package main\nfunc f() { x := []int{{1,2},{3,4}} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ShortVarDeclarationSyntax>(fn.Body!.Statements[0]);
        var decl = (ShortVarDeclarationSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralSyntax>(decl.Right[0]);
        var lit = (CompositeLiteralSyntax)decl.Right[0];
        Assert.AreEqual(2, lit.Elements.Count);
        // Inner elements should be bare composite literals (null type)
        Assert.IsInstanceOfType<CompositeLiteralSyntax>(lit.Elements[0]);
        var inner = (CompositeLiteralSyntax)lit.Elements[0];
        Assert.IsNull(inner.Type);
    }

    [TestMethod]
    public void Nested_composite_literal_key_value()
    {
        var file = Parse("package main\nfunc f() { x := map[string]int{\"a\":{1,2}} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ShortVarDeclarationSyntax>(fn.Body!.Statements[0]);
        var decl = (ShortVarDeclarationSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralSyntax>(decl.Right[0]);
        var lit = (CompositeLiteralSyntax)decl.Right[0];
        Assert.IsInstanceOfType<KeyValueExpressionSyntax>(lit.Elements[0]);
        var kv = (KeyValueExpressionSyntax)lit.Elements[0];
        Assert.IsInstanceOfType<CompositeLiteralSyntax>(kv.Value);
        var value = (CompositeLiteralSyntax)kv.Value;
        Assert.IsNull(value.Type);
    }

    [TestMethod]
    public void Switch_statement()
    {
        var file = Parse("package main\nfunc f() { switch x { case 1: return\ndefault: return\n} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<SwitchStatementSyntax>(fn.Body!.Statements[0]);
        var sw = (SwitchStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(2, sw.Cases.Count);
    }

    [TestMethod]
    public void Select_statement()
    {
        var file = Parse("package main\nfunc f() { select { default: return\n} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<SelectStatementSyntax>(fn.Body!.Statements[0]);
        var sel = (SelectStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(1, sel.Clauses.Count);
    }

    [TestMethod]
    public void Go_statement()
    {
        var file = Parse("package main\nfunc f() { go handle() }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<GoStatementSyntax>(fn.Body!.Statements[0]);
        var goStmt = (GoStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CallExpressionSyntax>(goStmt.Expression);
    }

    [TestMethod]
    public void Defer_statement()
    {
        var file = Parse("package main\nfunc f() { defer close() }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<DeferStatementSyntax>(fn.Body!.Statements[0]);
        var deferStmt = (DeferStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CallExpressionSyntax>(deferStmt.Expression);
    }

    [TestMethod]
    public void Short_var_declaration()
    {
        var file = Parse("package main\nfunc f() { x := 42 }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ShortVarDeclarationSyntax>(fn.Body!.Statements[0]);
        var shortVar = (ShortVarDeclarationSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(1, shortVar.Left.Count);
        Assert.AreEqual(1, shortVar.Right.Count);
    }

    [TestMethod]
    public void Assignment()
    {
        var file = Parse("package main\nfunc f() { x = 42 }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(fn.Body!.Statements[0]);
        var assign = (AssignmentStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(SyntaxKind.EqualsToken, assign.OperatorToken.Kind);
    }

    [TestMethod]
    public void Increment()
    {
        var file = Parse("package main\nfunc f() { x++ }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<IncDecStatementSyntax>(fn.Body!.Statements[0]);
        var inc = (IncDecStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(SyntaxKind.PlusPlusToken, inc.OperatorToken.Kind);
    }

    [TestMethod]
    public void Branch_statements()
    {
        var file = Parse("package main\nfunc f() { break }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<BranchStatementSyntax>(fn.Body!.Statements[0]);
        var branch = (BranchStatementSyntax)fn.Body!.Statements[0];
        Assert.AreEqual(SyntaxKind.BreakKeyword, branch.Keyword.Kind);
    }

    // ================================================================
    // Expressions
    // ================================================================

    [TestMethod]
    public void Binary_expression()
    {
        var file = Parse("package main\nfunc f() { x + y }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(exprStmt.Expression);
        var binary = (BinaryExpressionSyntax)exprStmt.Expression;
        Assert.AreEqual(SyntaxKind.PlusToken, binary.OperatorToken.Kind);
    }

    [TestMethod]
    public void Precedence_mul_over_add()
    {
        var file = Parse("package main\nfunc f() { x + y * z }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(exprStmt.Expression);
        var binary = (BinaryExpressionSyntax)exprStmt.Expression;
        // x + (y * z): top-level is +, right is *
        Assert.AreEqual(SyntaxKind.PlusToken, binary.OperatorToken.Kind);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(binary.Right);
    }

    [TestMethod]
    public void Unary_expression()
    {
        var file = Parse("package main\nfunc f() { -x }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<UnaryExpressionSyntax>(exprStmt.Expression);
        var unary = (UnaryExpressionSyntax)exprStmt.Expression;
        Assert.AreEqual(SyntaxKind.MinusToken, unary.OperatorToken.Kind);
    }

    [TestMethod]
    public void Function_call()
    {
        var file = Parse("package main\nfunc f() { fmt.Println(\"hello\") }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CallExpressionSyntax>(exprStmt.Expression);
        var call = (CallExpressionSyntax)exprStmt.Expression;
        Assert.IsInstanceOfType<SelectorExpressionSyntax>(call.Function);
        Assert.AreEqual(1, call.Arguments.Count);
    }

    [TestMethod]
    public void Index_expression()
    {
        var file = Parse("package main\nfunc f() { a[0] }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<IndexExpressionSyntax>(exprStmt.Expression);
        var index = (IndexExpressionSyntax)exprStmt.Expression;
        Assert.AreEqual("a", ((IdentifierNameSyntax)index.Expression).Identifier.Text);
    }

    [TestMethod]
    public void Slice_expression()
    {
        var file = Parse("package main\nfunc f() { a[1:3] }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<SliceExpressionSyntax>(exprStmt.Expression);
        var slice = (SliceExpressionSyntax)exprStmt.Expression;
        Assert.IsNotNull(slice.Low);
        Assert.IsNotNull(slice.High);
        Assert.IsNull(slice.Max);
    }

    [TestMethod]
    public void Composite_literal()
    {
        var file = Parse("package main\nfunc f() { Point{1, 2} }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralSyntax>(exprStmt.Expression);
        var lit = (CompositeLiteralSyntax)exprStmt.Expression;
        Assert.IsNotNull(lit.Type);
        Assert.AreEqual(2, lit.Elements.Count);
    }

    [TestMethod]
    public void Parenthesized_expression()
    {
        var file = Parse("package main\nfunc f() { (x + y) }");
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var exprStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<ParenthesizedExpressionSyntax>(exprStmt.Expression);
        var paren = (ParenthesizedExpressionSyntax)exprStmt.Expression;
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(paren.Expression);
    }

    // ================================================================
    // Type syntax
    // ================================================================

    [TestMethod]
    public void Pointer_type()
    {
        var file = Parse("package main\nvar x *int");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<PointerTypeSyntax>(varDecl.Specs[0].Type);
    }

    [TestMethod]
    public void Slice_type()
    {
        var file = Parse("package main\nvar x []int");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<SliceTypeSyntax>(varDecl.Specs[0].Type);
    }

    [TestMethod]
    public void Map_type()
    {
        var file = Parse("package main\nvar x map[string]int");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<MapTypeSyntax>(varDecl.Specs[0].Type);
    }

    [TestMethod]
    public void Channel_type()
    {
        var file = Parse("package main\nvar x chan int");
        Assert.IsInstanceOfType<VarDeclarationSyntax>(file.Members[0]);
        var varDecl = (VarDeclarationSyntax)file.Members[0];
        Assert.IsInstanceOfType<ChannelTypeSyntax>(varDecl.Specs[0].Type);
    }

    // ================================================================
    // Round-trip
    // ================================================================

    [DataTestMethod]
    [DataRow("package main")]
    [DataRow("package main\nimport \"fmt\"")]
    [DataRow("package main\nfunc main() {}")]
    [DataRow("package main\nvar x int")]
    [DataRow("package main\nconst pi = 3.14")]
    public void Round_trip_preserves_source(string source)
    {
        var file = Parse(source);

        var sb = new StringBuilder();
        foreach (var token in file.DescendantTokens())
        {
            foreach (var trivia in token.LeadingExtra)
                sb.Append(trivia.Text);
            sb.Append(token.Text);
            foreach (var trivia in token.TrailingExtra)
                sb.Append(trivia.Text);
        }

        Assert.AreEqual(source, sb.ToString());
    }

    // ================================================================
    // Hello world end-to-end
    // ================================================================

    [TestMethod]
    public void Hello_world_program()
    {
        var source = @"package main

import ""fmt""

func main() {
	fmt.Println(""Hello, world!"")
}";

        var file = Parse(source);
        Assert.AreEqual("main", file.PackageClause.Name.Text);
        Assert.AreEqual(1, file.Imports.Count);
        Assert.AreEqual(1, file.Members.Count);

        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(file.Members[0]);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.AreEqual("main", fn.Name.Text);
        Assert.AreEqual(1, fn.Body!.Statements.Count);

        Assert.IsInstanceOfType<ExpressionStatementSyntax>(fn.Body!.Statements[0]);
        var callStmt = (ExpressionStatementSyntax)fn.Body!.Statements[0];
        Assert.IsInstanceOfType<CallExpressionSyntax>(callStmt.Expression);
        var call = (CallExpressionSyntax)callStmt.Expression;
        Assert.IsInstanceOfType<SelectorExpressionSyntax>(call.Function);
        var selector = (SelectorExpressionSyntax)call.Function;
        Assert.AreEqual("fmt", ((IdentifierNameSyntax)selector.Expression).Identifier.Text);
        Assert.AreEqual("Println", selector.Name.Text);
    }
}
