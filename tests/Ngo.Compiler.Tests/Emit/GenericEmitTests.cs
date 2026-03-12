// -----------------------------------------------------------------------
// <copyright file="GenericEmitTests.cs" company="Ziad">
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
public class GenericEmitTests
{
    private static string Run(string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(null);
        var result = SemanticAnalyzer.Analyze(tree, ctx);

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
    // Generic functions with explicit type args
    // ================================================================

    [TestMethod]
    public void Generic_identity_int()
    {
        var output = Run(@"
package main

import ""fmt""

func Identity[T any](x T) T {
    return x
}

func main() {
    fmt.Println(Identity[int](42))
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Generic_identity_string()
    {
        var output = Run(@"
package main

import ""fmt""

func Identity[T any](x T) T {
    return x
}

func main() {
    fmt.Println(Identity[string](""hello""))
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Generic functions with type inference
    // ================================================================

    [TestMethod]
    public void Generic_identity_inferred_int()
    {
        var output = Run(@"
package main

import ""fmt""

func Identity[T any](x T) T {
    return x
}

func main() {
    fmt.Println(Identity(42))
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Generic_identity_inferred_string()
    {
        var output = Run(@"
package main

import ""fmt""

func Identity[T any](x T) T {
    return x
}

func main() {
    fmt.Println(Identity(""world""))
}
");
        Assert.AreEqual("world\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Two type parameters
    // ================================================================

    [TestMethod]
    public void Generic_two_type_params_explicit()
    {
        var output = Run(@"
package main

import ""fmt""

func First[T any, U any](a T, b U) T {
    return a
}

func main() {
    fmt.Println(First[int, string](99, ""ignored""))
}
");
        Assert.AreEqual("99\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Generic_two_type_params_inferred()
    {
        var output = Run(@"
package main

import ""fmt""

func Second[T any, U any](a T, b U) U {
    return b
}

func main() {
    fmt.Println(Second(1, ""hello""))
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Multiple calls with different type args
    // ================================================================

    [TestMethod]
    public void Generic_function_called_with_different_types()
    {
        var output = Run(@"
package main

import ""fmt""

func Echo[T any](x T) T {
    return x
}

func main() {
    fmt.Println(Echo[int](1))
    fmt.Println(Echo[string](""two""))
    fmt.Println(Echo[bool](true))
}
");
        Assert.AreEqual("1\ntwo\ntrue\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Comparable constraint
    // ================================================================

    [TestMethod]
    public void Generic_comparable_constraint()
    {
        var output = Run(@"
package main

import ""fmt""

func Max[T comparable](a T, b T) T {
    return a
}

func main() {
    fmt.Println(Max[int](3, 5))
}
");
        Assert.AreEqual("3\n", output.Replace("\r\n", "\n"));
    }
}
