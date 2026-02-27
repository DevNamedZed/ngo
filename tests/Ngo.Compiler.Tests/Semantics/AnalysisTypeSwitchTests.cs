// -----------------------------------------------------------------------
// <copyright file="AnalysisTypeSwitchTests.cs" company="Ziad">
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
public class AnalysisTypeSwitchTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Basic_type_switch_with_assignment()
    {
        var result = Analyze(@"package main
type Any interface {}
func main() {
    var x Any = 42
    switch v := x.(type) {
    case int:
        y := v + 1
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_switch_default_branch()
    {
        var result = Analyze(@"package main
type Any interface {}
func main() {
    var x Any = 42
    switch v := x.(type) {
    case int:
        y := v + 1
    default:
        println(v)
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_switch_struct_case_with_field_access()
    {
        var result = Analyze(@"package main
type Any interface {}
type Point struct { X int; Y int }
func main() {
    var x Any = Point{X: 1, Y: 2}
    switch v := x.(type) {
    case Point:
        a := v.X + v.Y
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_switch_without_assignment()
    {
        var result = Analyze(@"package main
type Any interface {}
func main() {
    var x Any = 42
    switch x.(type) {
    case int:
        println(1)
    case string:
        println(2)
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_switch_non_interface_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    switch x.(type) {
    case int:
        println(1)
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidTypeAssert));
    }

    [TestMethod]
    public void Type_switch_multiple_cases()
    {
        var result = Analyze(@"package main
type Any interface {}
func main() {
    var x Any = ""hello""
    switch v := x.(type) {
    case int:
        y := v + 1
    case string:
        y := v + "" world""
    case bool:
        println(v)
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_switch_with_init_statement()
    {
        var result = Analyze(@"package main
type Any interface {}
func getVal() Any { return 42 }
func main() {
    switch x := getVal(); v := x.(type) {
    case int:
        y := v + 1
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Parser_produces_type_switch_syntax()
    {
        var tree = SyntaxTree.Parse(@"package main
type Any interface {}
func main() {
    var x Any = 42
    switch v := x.(type) {
    case int:
        println(v)
    }
}");
        Assert.AreEqual(0, tree.Errors.Count);
    }
}
