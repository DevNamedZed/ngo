// -----------------------------------------------------------------------
// <copyright file="AnalysisStatementTests.cs" company="Ziad">
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

using System.Linq;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Semantics;

[TestClass]
public class AnalysisStatementTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }
    [TestMethod]
    public void Bind_return_with_value()
    {
        var result = Analyze("package main\nfunc f() int { return 42 }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsNotNull(ret.Value);
    }

    [TestMethod]
    public void Bind_return_void()
    {
        var result = Analyze("package main\nfunc f() { return }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsNull(ret.Value);
    }

    [TestMethod]
    public void Bind_return_type_mismatch()
    {
        var result = Analyze("package main\nfunc f() int { return \"hello\" }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_missing_return_value()
    {
        var result = Analyze("package main\nfunc f() int { return }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }
    [TestMethod]
    public void Bind_short_var_declaration()
    {
        var result = Analyze("package main\nfunc f() int { x := 42\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.AreEqual("x", varDecl.Symbol.Name);
        Assert.AreEqual(TypeKind.Int, varDecl.Symbol.Type.TypeKind);
        Assert.IsNotNull(varDecl.Initializer);
    }

    [TestMethod]
    public void Bind_short_var_infers_type_from_expression()
    {
        var result = Analyze("package main\nfunc f() string { x := \"hello\"\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.AreEqual(TypeKind.String, varDecl.Symbol.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_short_var_duplicate_reports_error()
    {
        var result = Analyze("package main\nfunc f() { x := 1\nx := 2 }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.AlreadyDeclared));
    }
    [TestMethod]
    public void Bind_assignment()
    {
        var result = Analyze("package main\nfunc f() int { x := 1\nx = 2\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<AssignmentStatement>(fn.Body.Statements[1]);
        var assign = (AssignmentStatement)fn.Body.Statements[1];
        Assert.IsInstanceOfType<IdentifierExpression>(assign.Target);
        var target = (IdentifierExpression)assign.Target;
        Assert.AreEqual("x", target.Symbol.Name);
    }

    [TestMethod]
    public void Bind_assignment_type_mismatch()
    {
        var result = Analyze("package main\nfunc f() { x := 1\nx = \"hello\" }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }
    [TestMethod]
    public void Bind_increment()
    {
        var result = Analyze("package main\nfunc f() int { x := 0\nx++\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IncDecStatement>(fn.Body.Statements[1]);
        var incDec = (IncDecStatement)fn.Body.Statements[1];
        Assert.IsTrue(incDec.IsIncrement);
    }

    [TestMethod]
    public void Bind_decrement()
    {
        var result = Analyze("package main\nfunc f() int { x := 0\nx--\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IncDecStatement>(fn.Body.Statements[1]);
        var incDec = (IncDecStatement)fn.Body.Statements[1];
        Assert.IsFalse(incDec.IsIncrement);
    }
    [TestMethod]
    public void Bind_expression_statement()
    {
        var result = Analyze("package main\nfunc noop() {}\nfunc f() { noop() }");
        Assert.IsFalse(result.HasErrors);
        var fFunc = result.Root.Functions.First(f => f.Symbol.Name == "f");
        Assert.IsInstanceOfType<ExpressionStatement>(fFunc.Body.Statements[0]);
        var stmt = (ExpressionStatement)fFunc.Body.Statements[0];
        Assert.IsInstanceOfType<CallExpression>(stmt.Expression);
    }
    [TestMethod]
    public void Bind_var_with_type_and_initializer()
    {
        var result = Analyze("package main\nfunc f() int { var x int = 42\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.AreEqual("x", varDecl.Symbol.Name);
        Assert.AreEqual(TypeKind.Int, varDecl.Symbol.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_var_with_type_only()
    {
        var result = Analyze("package main\nfunc f() int { var x int\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.AreEqual(TypeKind.Int, varDecl.Symbol.Type.TypeKind);
        Assert.IsNull(varDecl.Initializer);
    }

    [TestMethod]
    public void Bind_var_with_initializer_only()
    {
        var result = Analyze("package main\nfunc f() int { var x = 42\nreturn x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.AreEqual(TypeKind.Int, varDecl.Symbol.Type.TypeKind);
    }
    [TestMethod]
    public void Bind_nested_block_scope()
    {
        var result = Analyze("package main\nfunc f() int { x := 1\n{ x = 2 }\nreturn x }");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Short_var_redeclaration_valid()
    {
        var result = Analyze(@"package main
func pair() (int, string) { return 1, ""hi"" }
func main() {
    x := 10
    x, y := pair()
    _ = x
    _ = y
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Short_var_redeclaration_no_new_vars_error()
    {
        var result = Analyze(@"package main
func pair() (int, string) { return 1, ""hi"" }
func main() {
    x := 10
    y := ""hello""
    x, y := pair()
}");
        Assert.IsTrue(result.HasErrors);
    }
}
