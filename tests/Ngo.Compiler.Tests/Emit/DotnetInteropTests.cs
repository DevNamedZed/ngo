// -----------------------------------------------------------------------
// <copyright file="DotnetInteropTests.cs" company="Ziad">
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
public class DotnetInteropTests
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

    // ================================================================
    // dotnet.CallStatic
    // ================================================================

    [TestMethod]
    public void Dotnet_CallStatic_Math_Max()
    {
        var output = Run(@"
package main

import ""fmt""
import ""dotnet""

func main() {
    result := dotnet.CallStatic(""System.Math"", ""Max"", 3, 7)
    fmt.Println(result)
}");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Dotnet_CallStatic_String_Concat()
    {
        var output = Run(@"
package main

import ""fmt""
import ""dotnet""

func main() {
    result := dotnet.CallStatic(""System.String"", ""Concat"", ""Hello"", "" World"")
    fmt.Println(result)
}");
        Assert.AreEqual("Hello World\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // dotnet.New and dotnet.CallMethod
    // ================================================================

    [TestMethod]
    public void Dotnet_New_and_CallMethod()
    {
        var output = Run(@"
package main

import ""fmt""
import ""dotnet""

func main() {
    list := dotnet.New(""System.Collections.Generic.List`1[System.String]"")
    dotnet.CallMethod(list, ""Add"", ""hello"")
    dotnet.CallMethod(list, ""Add"", ""world"")
    count := dotnet.GetProperty(list, ""Count"")
    fmt.Println(count)
}");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // dotnet.TypeName
    // ================================================================

    [TestMethod]
    public void Dotnet_TypeName()
    {
        var output = Run(@"
package main

import ""fmt""
import ""dotnet""

func main() {
    name := dotnet.TypeName(""hello"")
    fmt.Println(name)
}");
        Assert.AreEqual("System.String\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // dotnet.GetStaticProperty
    // ================================================================

    [TestMethod]
    public void Dotnet_GetStaticProperty()
    {
        var output = Run(@"
package main

import ""fmt""
import ""dotnet""

func main() {
    newline := dotnet.GetStaticProperty(""System.Environment"", ""NewLine"")
    fmt.Print(""got newline: "")
    fmt.Println(len(newline.(string)) > 0)
}");
        Assert.AreEqual("got newline: true\n", output.Replace("\r\n", "\n"));
    }
}
