// -----------------------------------------------------------------------
// <copyright file="AnalysisUnusedTests.cs" company="Ziad">
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
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Semantics;

[TestClass]
public class AnalysisUnusedTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree, new CompilationContext(null), checkUnused: true);
    }

    [TestMethod]
    public void Unused_variable_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UnusedVariable && e.Message.Contains("'x'")));
    }

    [TestMethod]
    public void Used_variable_no_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    println(x)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Unused_import_error()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    println(42)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UnusedImport && e.Message.Contains("'fmt'")));
    }

    [TestMethod]
    public void Used_import_no_error()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Println(42)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Blank_identifier_not_reported()
    {
        var result = Analyze(@"package main
func main() {
    _ = 42
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Blank_import_not_reported()
    {
        var result = Analyze(@"package main
import _ ""fmt""
func main() {}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Assigned_but_not_read_is_unused()
    {
        var result = Analyze(@"package main
func main() {
    var x int
    x = 10
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UnusedVariable && e.Message.Contains("'x'")));
    }

    [TestMethod]
    public void Multiple_unused_variables()
    {
        var result = Analyze(@"package main
func main() {
    a := 1
    b := 2
    println(a)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UnusedVariable && e.Message.Contains("'b'")));
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UnusedVariable && e.Message.Contains("'a'")));
    }

    [TestMethod]
    public void For_range_key_used()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
    for i, v := range s {
        println(i, v)
    }
}");
        Assert.IsFalse(result.HasErrors);
    }
}
