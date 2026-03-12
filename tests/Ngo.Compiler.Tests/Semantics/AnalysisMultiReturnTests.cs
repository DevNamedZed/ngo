// -----------------------------------------------------------------------
// <copyright file="AnalysisMultiReturnTests.cs" company="Ziad">
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
public class AnalysisMultiReturnTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
    }

    [TestMethod]
    public void Function_with_two_return_types()
    {
        var result = Analyze(@"package main
func divide(a int, b int) (int, int) { return a / b, a % b }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        var func = result.Root.Functions.First(f => f.Symbol.Name == "divide");
        Assert.AreEqual(2, func.Symbol.ReturnTypes.Count);
        Assert.AreEqual(BuiltinTypes.Int, func.Symbol.ReturnTypes[0]);
        Assert.AreEqual(BuiltinTypes.Int, func.Symbol.ReturnTypes[1]);
    }

    [TestMethod]
    public void Function_with_three_return_types()
    {
        var result = Analyze(@"package main
func triple() (int, string, bool) { return 1, ""hi"", true }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        var func = result.Root.Functions.First(f => f.Symbol.Name == "triple");
        Assert.AreEqual(3, func.Symbol.ReturnTypes.Count);
    }

    [TestMethod]
    public void Return_two_values_matches_signature()
    {
        var result = Analyze(@"package main
func swap(a int, b int) (int, int) { return b, a }
func main() {}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Return_wrong_count_gives_error()
    {
        var result = Analyze(@"package main
func foo() (int, int) { return 1 }
func main() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongReturnCount));
    }

    [TestMethod]
    public void Return_type_mismatch_in_multi_return()
    {
        var result = Analyze(@"package main
func foo() (int, string) { return 1, 2 }
func main() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Short_var_unpacks_multi_return_call()
    {
        var result = Analyze(@"package main
func divide(a int, b int) (int, int) { return a / b, a % b }
func main() {
    q, r := divide(10, 3)
    _ = q
    _ = r
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Short_var_unpack_wrong_count_falls_through()
    {
        var result = Analyze(@"package main
func single() int { return 1 }
func main() {
    a, b := single()
}");
        // single() returns 1 value but LHS has 2 — will not match multi-return path
        // Falls through to pair-by-pair, mismatch count leads to only 'a' getting single()
        Assert.IsTrue(result.HasErrors);
    }

    [TestMethod]
    public void Assignment_unpacks_multi_return_call()
    {
        var result = Analyze(@"package main
func divide(a int, b int) (int, int) { return a / b, a % b }
func main() {
    var q int
    var r int
    q, r = divide(10, 3)
    _ = q
    _ = r
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_with_multiple_return_types()
    {
        var result = Analyze(@"package main
type Calc struct { Base int }
func (c Calc) DivMod(n int) (int, int) { return c.Base / n, c.Base % n }
func main() {
    c := Calc{Base: 10}
    q, r := c.DivMod(3)
    _ = q
    _ = r
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Void_function_has_empty_return_types()
    {
        var result = Analyze(@"package main
func noop() {}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        var func = result.Root.Functions.First(f => f.Symbol.Name == "noop");
        Assert.AreEqual(0, func.Symbol.ReturnTypes.Count);
    }

    [TestMethod]
    public void Single_return_backward_compat()
    {
        var result = Analyze(@"package main
func add(a int, b int) int { return a + b }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        var func = result.Root.Functions.First(f => f.Symbol.Name == "add");
        Assert.AreEqual(1, func.Symbol.ReturnTypes.Count);
        Assert.AreEqual(BuiltinTypes.Int, func.Symbol.ReturnType);
    }

    [TestMethod]
    public void Too_many_return_values_for_void()
    {
        var result = Analyze(@"package main
func foo() { return 1 }
func main() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongReturnCount));
    }

    [TestMethod]
    public void Multi_return_forward_reference()
    {
        var result = Analyze(@"package main
func main() {
    a, b := pair()
    _ = a
    _ = b
}
func pair() (int, string) { return 1, ""hello"" }");
        Assert.IsFalse(result.HasErrors);
    }
}
