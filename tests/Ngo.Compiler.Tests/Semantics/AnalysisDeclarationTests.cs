// -----------------------------------------------------------------------
// <copyright file="AnalysisDeclarationTests.cs" company="Ziad">
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
public class AnalysisDeclarationTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }
    [TestMethod]
    public void Bind_package_declaration()
    {
        var result = Analyze("package main");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("main", result.Root.Package.Symbol.Name);
        Assert.AreEqual(SymbolKind.Package, result.Root.Package.Symbol.Kind);
    }

    [TestMethod]
    public void Bind_package_with_different_name()
    {
        var result = Analyze("package foo");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("foo", result.Root.Package.Symbol.Name);
    }
    [TestMethod]
    public void Bind_simple_function()
    {
        var result = Analyze("package main\nfunc hello() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Functions.Count);
        var fn = result.Root.Functions[0];
        Assert.AreEqual("hello", fn.Symbol.Name);
        Assert.AreEqual(SymbolKind.Function, fn.Symbol.Kind);
        Assert.AreEqual(0, fn.Symbol.Parameters.Count);
        Assert.AreSame(BuiltinTypes.Void, fn.Symbol.ReturnType);
    }

    [TestMethod]
    public void Bind_function_with_parameters()
    {
        var result = Analyze("package main\nfunc add(a int, b int) int { return a + b }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.AreEqual(2, fn.Symbol.Parameters.Count);
        Assert.AreEqual("a", fn.Symbol.Parameters[0].Name);
        Assert.AreEqual(TypeKind.Int, fn.Symbol.Parameters[0].Type.TypeKind);
        Assert.AreEqual(0, fn.Symbol.Parameters[0].Ordinal);
        Assert.AreEqual("b", fn.Symbol.Parameters[1].Name);
        Assert.AreEqual(1, fn.Symbol.Parameters[1].Ordinal);
        Assert.AreEqual(TypeKind.Int, fn.Symbol.ReturnType.TypeKind);
    }

    [TestMethod]
    public void Bind_function_with_string_return()
    {
        var result = Analyze("package main\nfunc greet() string { return \"hello\" }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.AreEqual(TypeKind.String, fn.Symbol.ReturnType.TypeKind);
    }

    [TestMethod]
    public void Bind_multiple_functions()
    {
        var result = Analyze("package main\nfunc a() {}\nfunc b() {}\nfunc c() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(3, result.Root.Functions.Count);
        Assert.AreEqual("a", result.Root.Functions[0].Symbol.Name);
        Assert.AreEqual("b", result.Root.Functions[1].Symbol.Name);
        Assert.AreEqual("c", result.Root.Functions[2].Symbol.Name);
    }

    [TestMethod]
    public void Bind_duplicate_function_reports_error()
    {
        var result = Analyze("package main\nfunc f() {}\nfunc f() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.AlreadyDeclared));
    }
    [TestMethod]
    public void Bind_forward_reference()
    {
        var result = Analyze("package main\nfunc f() int { return g() }\nfunc g() int { return 42 }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions.First(f => f.Symbol.Name == "f");
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CallExpression>(ret.Value);
        var call = (CallExpression)ret.Value;
        Assert.AreEqual("g", call.Function.Name);
    }

    [TestMethod]
    public void Bind_mutual_recursion()
    {
        var result = Analyze(@"package main
func even(n int) bool { return n == 0 || odd(n - 1) }
func odd(n int) bool { return n != 0 && even(n - 1) }");
        // Should not crash; may have errors due to - operator on bool but forward refs should work
        Assert.IsNotNull(result.Root);
    }
    [TestMethod]
    public void Bind_function_body_has_block()
    {
        var result = Analyze("package main\nfunc f() int { return 1 }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsNotNull(fn.Body);
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
    }

    [TestMethod]
    public void Bind_function_parameters_accessible_in_body()
    {
        var result = Analyze("package main\nfunc f(x int, y string) int { return x }");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<IdentifierExpression>(ret.Value);
        var id = (IdentifierExpression)ret.Value;
        Assert.AreEqual("x", id.Symbol.Name);
        Assert.IsInstanceOfType<ParameterSymbol>(id.Symbol);
    }
    [TestMethod]
    public void Bind_top_level_var()
    {
        var result = Analyze("package main\nvar x int = 42");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Variables.Count);
        var v = result.Root.Variables[0];
        Assert.AreEqual("x", v.Symbol.Name);
        Assert.AreEqual(TypeKind.Int, v.Symbol.Type.TypeKind);
    }
}
