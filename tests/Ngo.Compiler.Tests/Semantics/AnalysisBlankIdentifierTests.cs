// -----------------------------------------------------------------------
// <copyright file="AnalysisBlankIdentifierTests.cs" company="Ziad">
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
public class AnalysisBlankIdentifierTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Blank_short_var_no_variable_declared()
    {
        var result = Analyze(@"package main
func main() {
    _ := 42
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Blank_with_multi_return_only_second_declared()
    {
        var result = Analyze(@"package main
func pair() (int, string) { return 1, ""hello"" }
func main() {
    _, s := pair()
    _ = s
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Both_blank_in_multi_return()
    {
        var result = Analyze(@"package main
func pair() (int, string) { return 1, ""hello"" }
func main() {
    _, _ := pair()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Blank_assignment_discard()
    {
        var result = Analyze(@"package main
func main() {
    _ = 42
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void For_range_with_blank_key()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
    for _, v := range s {
        _ = v
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Blank_in_multi_return_assignment()
    {
        var result = Analyze(@"package main
func pair() (int, string) { return 1, ""hello"" }
func main() {
    var x string
    _, x = pair()
    _ = x
}");
        // Note: assignment with multi-return and blank on LHS
        // The blank skips, x gets the second return value (string)
        Assert.IsFalse(result.HasErrors);
    }
}
