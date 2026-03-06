// -----------------------------------------------------------------------
// <copyright file="MultiPackageEmitTests.cs" company="Ziad">
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
public class MultiPackageEmitTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ngo_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        PackageRegistry.SetProjectRoot(null);
    }

    private string Run(string mainSource)
    {
        // Write main.go
        File.WriteAllText(Path.Combine(_tempDir, "main.go"), mainSource);

        // Set project root so PackageRegistry can find user packages
        PackageRegistry.SetProjectRoot(_tempDir);

        var tree = SyntaxTree.Parse(mainSource);
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

    private void WritePackageFile(string pkgPath, string fileName, string content)
    {
        var dir = Path.Combine(_tempDir, pkgPath);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    // ================================================================
    // User package imports
    // ================================================================

    [TestMethod]
    public void Import_user_package_function()
    {
        WritePackageFile("mymath", "mymath.go", @"package mymath

func Add(a, b int) int {
    return a + b
}");

        var output = Run(@"package main

import ""fmt""
import ""mymath""

func main() {
    fmt.Println(mymath.Add(3, 4))
}");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Import_user_package_constant()
    {
        WritePackageFile("config", "config.go", @"package config

const Version = ""1.0""
");

        var output = Run(@"package main

import ""fmt""
import ""config""

func main() {
    fmt.Println(config.Version)
}");
        Assert.AreEqual("1.0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Import_user_package_type()
    {
        WritePackageFile("models", "models.go", @"package models

type Point struct {
    X int
    Y int
}

func NewPoint(x, y int) Point {
    return Point{X: x, Y: y}
}
");

        var output = Run(@"package main

import ""fmt""
import ""models""

func main() {
    p := models.NewPoint(10, 20)
    fmt.Println(p.X, p.Y)
}");
        Assert.AreEqual("10 20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void User_package_unexported_not_visible()
    {
        WritePackageFile("internal", "internal.go", @"package internal

func helper() int {
    return 42
}

func Public() int {
    return helper()
}
");

        var output = Run(@"package main

import ""fmt""
import ""internal""

func main() {
    fmt.Println(internal.Public())
}");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void User_package_multi_file()
    {
        WritePackageFile("utils", "add.go", @"package utils

func Add(a, b int) int {
    return a + b
}
");
        WritePackageFile("utils", "mul.go", @"package utils

func Mul(a, b int) int {
    return a * b
}
");

        var output = Run(@"package main

import ""fmt""
import ""utils""

func main() {
    fmt.Println(utils.Add(2, 3))
    fmt.Println(utils.Mul(4, 5))
}");
        Assert.AreEqual("5\n20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Unknown_user_package_reports_error()
    {
        PackageRegistry.SetProjectRoot(_tempDir);

        var tree = SyntaxTree.Parse(@"package main

import ""nonexistent""

func main() {}");

        var result = SemanticAnalyzer.Analyze(tree);
        Assert.IsTrue(result.HasErrors);
    }

    [TestMethod]
    public void Sub_directory_package()
    {
        WritePackageFile("lib/math", "math.go", @"package math

func Double(x int) int {
    return x * 2
}
");

        var output = Run(@"package main

import ""fmt""
import ""lib/math""

func main() {
    fmt.Println(math.Double(21))
}");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }
}
