// -----------------------------------------------------------------------
// <copyright file="AnalysisTests.cs" company="Ziad">
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
public class AnalysisTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }
    [TestMethod]
    public void Full_program_add_function()
    {
        var result = Analyze(@"package main

func add(a int, b int) int {
    return a + b
}

func main() {
    x := add(1, 2)
    y := x + 3
}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("main", result.Root.Package.Symbol.Name);
        Assert.AreEqual(2, result.Root.Functions.Count);

        var addFn = result.Root.Functions.First(f => f.Symbol.Name == "add");
        Assert.AreEqual(2, addFn.Symbol.Parameters.Count);
        Assert.AreEqual(TypeKind.Int, addFn.Symbol.ReturnType.TypeKind);

        var mainFn = result.Root.Functions.First(f => f.Symbol.Name == "main");
        Assert.AreEqual(2, mainFn.Body.Statements.Count);
    }

    [TestMethod]
    public void Full_program_with_var_and_assignment()
    {
        var result = Analyze(@"package main

func compute(n int) int {
    var result int = 0
    result = n * 2
    return result
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.AreEqual(3, fn.Body.Statements.Count);
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        Assert.IsInstanceOfType<AssignmentStatement>(fn.Body.Statements[1]);
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[2]);
    }

    [TestMethod]
    public void Full_program_with_type_conversion()
    {
        var result = Analyze(@"package main

func toFloat(x int) float64 {
    return float64(x)
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<ConversionExpression>(ret.Value);
        var conv = (ConversionExpression)ret.Value;
        Assert.AreEqual(TypeKind.Float64, conv.Type.TypeKind);
    }

    [TestMethod]
    public void Full_program_increment_loop_body()
    {
        var result = Analyze(@"package main

func inc(n int) int {
    n++
    return n
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.AreEqual(2, fn.Body.Statements.Count);
        Assert.IsInstanceOfType<IncDecStatement>(fn.Body.Statements[0]);
        var incDec = (IncDecStatement)fn.Body.Statements[0];
        Assert.IsTrue(incDec.IsIncrement);
    }

    [TestMethod]
    public void Full_program_forward_call()
    {
        var result = Analyze(@"package main

func caller() int {
    return callee(10)
}

func callee(x int) int {
    return x * 2
}");
        Assert.IsFalse(result.HasErrors);
        var caller = result.Root.Functions.First(f => f.Symbol.Name == "caller");
        var ret = (ReturnStatement)caller.Body.Statements[0];
        Assert.IsInstanceOfType<CallExpression>(ret.Value);
        var call = (CallExpression)ret.Value;
        Assert.AreEqual("callee", call.Function.Name);
    }

    [TestMethod]
    public void Full_program_multiple_parameters_and_types()
    {
        var result = Analyze(@"package main

func process(name string, count int, factor float64) float64 {
    return float64(count) * factor
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.AreEqual(3, fn.Symbol.Parameters.Count);
        Assert.AreEqual(TypeKind.String, fn.Symbol.Parameters[0].Type.TypeKind);
        Assert.AreEqual(TypeKind.Int, fn.Symbol.Parameters[1].Type.TypeKind);
        Assert.AreEqual(TypeKind.Float64, fn.Symbol.Parameters[2].Type.TypeKind);
    }

    [TestMethod]
    public void AnalysisResult_merges_parse_and_bind_errors()
    {
        // "package" without name → parse error. "xyz" → bind error (undefined)
        var result = Analyze("package\nfunc f() int { return xyz }");
        Assert.IsTrue(result.HasErrors);
        // Should have at least one parse error (TokenExpected) and one bind error (UndeclaredName)
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TokenExpected));
    }

    [TestMethod]
    public void AnalysisResult_no_errors_for_valid_program()
    {
        var result = Analyze("package main\nfunc f() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0, result.Errors.Where(e => e.Severity == ErrorSeverity.Error).Count());
    }

    [TestMethod]
    public void Ast_nodes_have_spans()
    {
        var result = Analyze("package main\nfunc f() int { return 42 }");
        Assert.IsFalse(result.HasErrors);

        // All nodes should have non-default spans
        var fn = result.Root.Functions[0];
        Assert.IsTrue(fn.Span.Length > 0);
        Assert.IsTrue(fn.Body.Span.Length > 0);

        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsTrue(ret.Span.Length > 0);
    }
}
