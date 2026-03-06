// -----------------------------------------------------------------------
// <copyright file="AnalysisGenericTests.cs" company="Ziad">
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
public class AnalysisGenericTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    // ================================================================
    // Generic function declarations
    // ================================================================

    [TestMethod]
    public void Generic_function_declaration_has_type_params()
    {
        var result = Analyze(@"package main

func Max[T any](a T, b T) T {
    return a
}

func main() {
    _ = Max[int](1, 2)
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        var maxFn = result.Root.Functions.First(f => f.Symbol.Name == "Max");
        Assert.IsTrue(maxFn.Symbol.IsGeneric);
        Assert.AreEqual(1, maxFn.Symbol.TypeParameters.Count);
        Assert.AreEqual("T", maxFn.Symbol.TypeParameters[0].Name);
    }

    [TestMethod]
    public void Generic_function_two_type_params()
    {
        var result = Analyze(@"package main

func Pair[T any, U any](a T, b U) T {
    return a
}

func main() {
    _ = Pair[int, string](1, ""hello"")
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        var fn = result.Root.Functions.First(f => f.Symbol.Name == "Pair");
        Assert.AreEqual(2, fn.Symbol.TypeParameters.Count);
        Assert.AreEqual("T", fn.Symbol.TypeParameters[0].Name);
        Assert.AreEqual("U", fn.Symbol.TypeParameters[1].Name);
    }

    // ================================================================
    // Type inference
    // ================================================================

    [TestMethod]
    public void Generic_function_type_inference()
    {
        var result = Analyze(@"package main

func Identity[T any](x T) T {
    return x
}

func main() {
    x := Identity(42)
    _ = x
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    [TestMethod]
    public void Generic_function_infers_string()
    {
        var result = Analyze(@"package main

func Echo[T any](x T) T {
    return x
}

func main() {
    s := Echo(""hello"")
    _ = s
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // Explicit type arguments
    // ================================================================

    [TestMethod]
    public void Explicit_single_type_arg()
    {
        var result = Analyze(@"package main

func Zero[T any]() T {
    var t T
    return t
}

func main() {
    _ = Zero[int]()
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // Constraint checking
    // ================================================================

    [TestMethod]
    public void Comparable_constraint_satisfied_by_int()
    {
        var result = Analyze(@"package main

func Equal[T comparable](a T, b T) bool {
    return false
}

func main() {
    _ = Equal[int](1, 2)
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    [TestMethod]
    public void Comparable_constraint_satisfied_by_string()
    {
        var result = Analyze(@"package main

func Equal[T comparable](a T, b T) bool {
    return false
}

func main() {
    _ = Equal[string](""a"", ""b"")
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // Wrong type argument count
    // ================================================================

    [TestMethod]
    public void Wrong_type_argument_count_reports_error()
    {
        var result = Analyze(@"package main

func Max[T any](a T, b T) T {
    return a
}

func main() {
    _ = Max[int, string](1, 2)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("type argument")),
            "Expected error about wrong type argument count");
    }

    // ================================================================
    // Generic struct declarations
    // ================================================================

    [TestMethod]
    public void Generic_struct_declaration()
    {
        var result = Analyze(@"package main

type Box[T any] struct {
    Value T
}

func main() {
}");
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        var typeDecl = result.Root.Types.First();
        var structType = typeDecl.Symbol as StructTypeSymbol;
        Assert.IsNotNull(structType);
        Assert.IsTrue(structType!.IsGeneric);
        Assert.AreEqual(1, structType.TypeParameters.Count);
        Assert.AreEqual("T", structType.TypeParameters[0].Name);
    }

    // ================================================================
    // Cannot infer type arguments
    // ================================================================

    [TestMethod]
    public void Cannot_infer_when_no_args_match_type_param()
    {
        var result = Analyze(@"package main

func Zero[T any]() T {
    var t T
    return t
}

func main() {
    _ = Zero()
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("infer")),
            "Expected error about cannot infer type arguments");
    }
}
