// -----------------------------------------------------------------------
// <copyright file="FunctionValueTests.cs" company="Ziad">
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
using System.IO;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class FunctionValueTests
{
    private static string Run(string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var result = SemanticAnalyzer.Analyze(tree);

        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var assembly = AssemblyEmitter.Emit(result);
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

        return sw.ToString();
    }

    [TestMethod]
    public void Named_function_value_simple()
    {
        var output = Run(@"
package main

import ""fmt""

func add(a, b int) int {
    return a + b
}

func main() {
    fn := add
    fmt.Println(fn(3, 4))
}");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_function_value_no_return()
    {
        var output = Run(@"
package main

import ""fmt""

func greet(name string) {
    fmt.Println(""hello"", name)
}

func main() {
    fn := greet
    fn(""world"")
}");
        Assert.AreEqual("hello world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_function_value_passed_as_argument()
    {
        var output = Run(@"
package main

import ""fmt""

func double(x int) int {
    return x * 2
}

func apply(f func(int) int, x int) int {
    return f(x)
}

func main() {
    fmt.Println(apply(double, 5))
}");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_function_value_reassigned()
    {
        var output = Run(@"
package main

import ""fmt""

func add(a, b int) int { return a + b }
func mul(a, b int) int { return a * b }

func main() {
    fn := add
    fmt.Println(fn(3, 4))
    fn = mul
    fmt.Println(fn(3, 4))
}");
        Assert.AreEqual("7\n12\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_function_value_in_slice()
    {
        var output = Run(@"
package main

import ""fmt""

func inc(x int) int { return x + 1 }
func dec(x int) int { return x - 1 }

func main() {
    fns := []func(int) int{inc, dec}
    for _, fn := range fns {
        fmt.Println(fn(10))
    }
}");
        Assert.AreEqual("11\n9\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_function_value_multi_return()
    {
        var output = Run(@"
package main

import ""fmt""

func divmod(a, b int) (int, int) {
    return a / b, a % b
}

func main() {
    fn := divmod
    q, r := fn(17, 5)
    fmt.Println(q, r)
}");
        Assert.AreEqual("3 2\n", output.Replace("\r\n", "\n"));
    }
}
