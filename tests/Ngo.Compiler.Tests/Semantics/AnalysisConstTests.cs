// -----------------------------------------------------------------------
// <copyright file="AnalysisConstTests.cs" company="Ziad">
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
public class AnalysisConstTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Simple_const_int()
    {
        var result = Analyze(@"package main
const x = 42
func main() {
    var y int = x
    _ = y
}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Constants.Count);
        Assert.AreEqual("x", result.Root.Constants[0].Symbol.Name);
    }

    [TestMethod]
    public void Simple_const_string()
    {
        var result = Analyze(@"package main
const greeting = ""hello""
func main() {
    var s string = greeting
    _ = s
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Const_with_explicit_type()
    {
        var result = Analyze(@"package main
const x int = 10
func main() {
    var y int = x
    _ = y
}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(BuiltinTypes.Int, result.Root.Constants[0].Symbol.Type);
    }

    [TestMethod]
    public void Const_block_multiple_specs()
    {
        var result = Analyze(@"package main
const (
    a = 1
    b = 2
    c = 3
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(3, result.Root.Constants.Count);
    }

    [TestMethod]
    public void Const_iota_basic()
    {
        var result = Analyze(@"package main
const (
    a = iota
    b
    c
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(3, result.Root.Constants.Count);
        Assert.AreEqual(0L, result.Root.Constants[0].Symbol.Value);
        Assert.AreEqual(1L, result.Root.Constants[1].Symbol.Value);
        Assert.AreEqual(2L, result.Root.Constants[2].Symbol.Value);
    }

    [TestMethod]
    public void Const_iota_with_expression()
    {
        var result = Analyze(@"package main
const (
    a = iota + 10
    b
    c
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(3, result.Root.Constants.Count);
        // iota+10: 0+10=10, 1+10=11, 2+10=12
        Assert.AreEqual(10L, result.Root.Constants[0].Symbol.Value);
        Assert.AreEqual(11L, result.Root.Constants[1].Symbol.Value);
        Assert.AreEqual(12L, result.Root.Constants[2].Symbol.Value);
    }

    [TestMethod]
    public void Const_used_in_expression()
    {
        var result = Analyze(@"package main
const limit = 100
func main() {
    x := limit + 1
    _ = x
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Const_duplicate_name_error()
    {
        // Duplicate const inside function body is an error
        var result = Analyze(@"package main
func main() {
    const x = 1
    const x = 2
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.AlreadyDeclared));
    }

    [TestMethod]
    public void Const_in_function_body()
    {
        var result = Analyze(@"package main
func main() {
    const x = 42
    var y int = x
    _ = y
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Iota_outside_const_error()
    {
        var result = Analyze(@"package main
func main() {
    x := iota
    _ = x
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Const_iota_multiply()
    {
        var result = Analyze(@"package main
const (
    a = iota * 2
    b
    c
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0L, result.Root.Constants[0].Symbol.Value);
        Assert.AreEqual(2L, result.Root.Constants[1].Symbol.Value);
        Assert.AreEqual(4L, result.Root.Constants[2].Symbol.Value);
    }

    [TestMethod]
    public void Top_level_constants_in_source_file()
    {
        var result = Analyze(@"package main
const (
    a = 1
    b = 2
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(2, result.Root.Constants.Count);
        Assert.AreEqual("a", result.Root.Constants[0].Symbol.Name);
        Assert.AreEqual("b", result.Root.Constants[1].Symbol.Name);
    }

    [TestMethod]
    public void Const_bitwise_and()
    {
        var result = Analyze(@"package main
const x = 0xFF & 0x0F
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(15L, result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_shift_left()
    {
        var result = Analyze(@"package main
const x = 1 << 3
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(8L, result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_iota_shift()
    {
        var result = Analyze(@"package main
const (
    a = 1 << iota
    b
    c
)
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(3, result.Root.Constants.Count);
        Assert.AreEqual(1L, result.Root.Constants[0].Symbol.Value);
        Assert.AreEqual(2L, result.Root.Constants[1].Symbol.Value);
        Assert.AreEqual(4L, result.Root.Constants[2].Symbol.Value);
    }

    [TestMethod]
    public void Const_bitwise_complement()
    {
        var result = Analyze(@"package main
const x = ^0
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(-1L, result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_float_multiply()
    {
        var result = Analyze(@"package main
const x = 3.14 * 2.0
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(6.28, result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_string_concat()
    {
        var result = Analyze(@"package main
const s = ""hello"" + "" world""
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual("hello world", result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_bool_logic()
    {
        var result = Analyze(@"package main
const x = true && false
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(false, result.Root.Constants[0].Symbol.Value);
    }

    [TestMethod]
    public void Const_reference()
    {
        var result = Analyze(@"package main
const a = 10
const b = a + 5
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(10L, result.Root.Constants[0].Symbol.Value);
        Assert.AreEqual(15L, result.Root.Constants[1].Symbol.Value);
    }
}
