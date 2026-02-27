// -----------------------------------------------------------------------
// <copyright file="AnalysisClosureTests.cs" company="Ziad">
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
public class AnalysisClosureTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Function_literal_no_params_no_return()
    {
        var result = Analyze(@"package main
func main() {
    f := func() {
        _ = 42
    }
    f()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Function_literal_with_params_and_return()
    {
        var result = Analyze(@"package main
func main() {
    add := func(a int, b int) int {
        return a + b
    }
    x := add(1, 2)
    _ = x
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Closure_captures_outer_variable()
    {
        var result = Analyze(@"package main
func main() {
    x := 10
    f := func() int {
        return x
    }
    y := f()
    _ = y
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Immediately_invoked_function_literal()
    {
        var result = Analyze(@"package main
func main() {
    x := func() int {
        return 42
    }()
    _ = x
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Wrong_arg_count_on_function_value_call_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    f := func(x int) int {
        return x
    }
    f(1, 2)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongArgumentCount));
    }

    [TestMethod]
    public void Arg_type_mismatch_on_function_value_call_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    f := func(x int) int {
        return x
    }
    f(""hello"")
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Function_literal_missing_return_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    f := func() int {
        _ = 42
    }
    _ = f
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }

    [TestMethod]
    public void Var_with_func_type_resolves()
    {
        var result = Analyze(@"package main
func main() {
    var f func(int) int
    _ = f
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Function_literal_type_is_FunctionTypeSymbol()
    {
        var result = Analyze(@"package main
func main() {
    f := func(x int) int {
        return x
    }
    _ = f
}");
        Assert.IsFalse(result.HasErrors);
        var mainFunc = result.Root.Functions[0];
        var body = mainFunc.Body;
        // First statement is f := ...
        var varDecl = body.Statements[0] as VarDeclaration;
        Assert.IsNotNull(varDecl);
        Assert.IsInstanceOfType<FunctionTypeSymbol>(varDecl!.Symbol.Type);
        var funcType = (FunctionTypeSymbol)varDecl.Symbol.Type;
        Assert.AreEqual(1, funcType.ParameterTypes.Count);
        Assert.AreEqual(TypeKind.Int, funcType.ParameterTypes[0].TypeKind);
        Assert.AreEqual(1, funcType.ReturnTypes.Count);
        Assert.AreEqual(TypeKind.Int, funcType.ReturnTypes[0].TypeKind);
    }

    [TestMethod]
    public void Nil_assignable_to_function_type()
    {
        var result = Analyze(@"package main
func main() {
    var f func(int) int
    f = nil
    _ = f
}");
        Assert.IsFalse(result.HasErrors);
    }
}
