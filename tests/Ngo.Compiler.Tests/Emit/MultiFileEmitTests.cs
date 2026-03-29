// -----------------------------------------------------------------------
// <copyright file="MultiFileEmitTests.cs" company="Ziad">
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

using System;
using System.Collections.Generic;
using System.IO;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class MultiFileEmitTests
{
    private static readonly string TestProjectRoot = Path.Combine(Path.GetTempPath(), "ngo-test-project");

    static MultiFileEmitTests()
    {
        Directory.CreateDirectory(TestProjectRoot);
    }

    private static string Run(params string[] goSources)
    {
        var trees = new List<SyntaxTree>();
        foreach (var src in goSources)
            trees.Add(SyntaxTree.Parse(src));

        var ctx = new CompilationContext(TestProjectRoot);
        var result = SemanticAnalyzer.Analyze(trees, ctx);
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var assembly = AssemblyEmitter.Emit(result, ctx);
        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        Assert.IsNotNull(entryPoint);

        var oldOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            entryPoint.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(oldOut);
        }

        return sw.ToString().Replace("\r\n", "\n");
    }

    // ================================================================
    // Multi-file same package
    // ================================================================

    [TestMethod]
    public void Two_files_share_function()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    fmt.Println(greet())
}",
            @"package main

func greet() string {
    return ""hello from file2""
}");
        Assert.AreEqual("hello from file2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Two_files_share_type()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    p := Point{X: 3, Y: 4}
    fmt.Println(p.X, p.Y)
}",
            @"package main

type Point struct {
    X int
    Y int
}");
        Assert.AreEqual("3 4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Two_files_share_constant()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    fmt.Println(MaxSize)
}",
            @"package main

const MaxSize = 100");
        Assert.AreEqual("100\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Two_files_share_variable()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    name = ""world""
    fmt.Println(name)
}",
            @"package main

var name string");
        Assert.AreEqual("world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Two_files_share_method()
    {
        var output = Run(
            @"package main

import ""fmt""

type Counter struct {
    Value int
}

func main() {
    c := Counter{Value: 5}
    c.Inc()
    fmt.Println(c.Value)
}",
            @"package main

func (c *Counter) Inc() {
    c.Value = c.Value + 1
}");
        Assert.AreEqual("6\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Three_files_all_contribute()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    fmt.Println(add(2, 3))
    fmt.Println(sub(5, 2))
}",
            @"package main

func add(a, b int) int {
    return a + b
}",
            @"package main

func sub(a, b int) int {
    return a - b
}");
        Assert.AreEqual("5\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Two_files_separate_imports()
    {
        var output = Run(
            @"package main

import ""fmt""

func main() {
    s := buildMessage()
    fmt.Println(s)
}",
            @"package main

import ""strconv""

func buildMessage() string {
    return ""value: "" + strconv.Itoa(42)
}");
        Assert.AreEqual("value: 42\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Error cases
    // ================================================================

    [TestMethod]
    public void Mismatched_package_names_reports_error()
    {
        var trees = new List<SyntaxTree>
        {
            SyntaxTree.Parse("package main\nfunc main() {}"),
            SyntaxTree.Parse("package other\nfunc helper() {}")
        };

        var result = SemanticAnalyzer.Analyze(trees, new CompilationContext(TestProjectRoot));
        Assert.IsTrue(result.HasErrors);
    }
}
