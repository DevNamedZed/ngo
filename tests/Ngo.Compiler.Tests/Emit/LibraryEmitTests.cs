// -----------------------------------------------------------------------
// <copyright file="LibraryEmitTests.cs" company="Ziad">
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
using System.Reflection;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class LibraryEmitTests
{
    private static Assembly EmitLibrary(string goSource, EmitOptions? options = null)
    {
        var tree = SyntaxTree.Parse(goSource);
        var result = SemanticAnalyzer.Analyze(tree);
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        return AssemblyEmitter.Emit(result, "TestLib", options);
    }

    [TestMethod]
    public void Library_emits_namespace()
    {
        var source = @"
package mylib

func Add(a int, b int) int {
    return a + b
}
";
        var options = new EmitOptions { IsLibrary = true, Namespace = "MyNs" };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("MyNs.mylib");
        Assert.IsNotNull(type, "Expected type MyNs.mylib");
        Assert.IsTrue(type.FullName == "MyNs.mylib");
    }

    [TestMethod]
    public void Library_exported_function_is_public()
    {
        var source = @"
package mylib

func Add(a int, b int) int {
    return a + b
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("mylib");
        Assert.IsNotNull(type);

        var method = type.GetMethod("Add", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "Exported function Add should be public");
    }

    [TestMethod]
    public void Library_unexported_function_is_internal()
    {
        var source = @"
package mylib

func helper(a int) int {
    return a + 1
}

func Add(a int) int {
    return helper(a)
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("mylib");
        Assert.IsNotNull(type);

        var publicMethod = type.GetMethod("helper", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(publicMethod, "Unexported function helper should not be public");

        var internalMethod = type.GetMethod("helper", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(internalMethod, "Unexported function helper should be internal");
    }

    [TestMethod]
    public void Library_exported_struct_is_public()
    {
        var source = @"
package mylib

type Point struct {
    X int
    Y int
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("Point");
        Assert.IsNotNull(type, "Exported struct Point should exist");
        Assert.IsTrue(type.IsPublic, "Exported struct Point should be public");
    }

    [TestMethod]
    public void Library_unexported_struct_fields()
    {
        var source = @"
package mylib

type Widget struct {
    Name string
    id   int
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("Widget");
        Assert.IsNotNull(type);

        var nameField = type.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(nameField, "Exported field Name should be public");

        var idPublic = type.GetField("id", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNull(idPublic, "Unexported field id should not be public");

        var idInternal = type.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(idInternal, "Unexported field id should be internal");
    }

    [TestMethod]
    public void Library_initialize_triggers_init()
    {
        var source = @"
package mylib

import ""fmt""

var Counter int

func init() {
    Counter = 42
}

func GetCounter() int {
    return Counter
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("mylib");
        Assert.IsNotNull(type);

        var initMethod = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(initMethod, "Library should have Initialize() method");

        // Capture stdout since init might print
        var oldOut = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            initMethod.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(oldOut);
        }

        var counterField = type.GetField("Counter", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(counterField);
        Assert.AreEqual((long)42, counterField.GetValue(null));
    }

    [TestMethod]
    public void Library_no_namespace_flat()
    {
        var source = @"
package mylib

func Add(a int, b int) int {
    return a + b
}
";
        var options = new EmitOptions { IsLibrary = true };
        var assembly = EmitLibrary(source, options);

        var type = assembly.GetType("mylib");
        Assert.IsNotNull(type, "Without namespace, type should be flat 'mylib'");
        Assert.AreEqual("mylib", type.FullName);
    }

    [TestMethod]
    public void Default_mode_all_public()
    {
        var source = @"
package main

import ""fmt""

var counter int

func helper() int {
    return counter + 1
}

func main() {
    counter = helper()
    fmt.Println(counter)
}
";
        var assembly = EmitLibrary(source, null);

        var type = assembly.GetType("main");
        Assert.IsNotNull(type);

        // In default (non-library) mode, everything should be public
        var helperMethod = type.GetMethod("helper", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(helperMethod, "In default mode, helper should be public");

        var counterField = type.GetField("counter", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(counterField, "In default mode, counter should be public");
    }
}
