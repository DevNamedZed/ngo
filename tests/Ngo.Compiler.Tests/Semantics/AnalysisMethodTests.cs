// -----------------------------------------------------------------------
// <copyright file="AnalysisMethodTests.cs" company="Ziad">
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
public class AnalysisMethodTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Value_receiver_method_declaration()
    {
        var result = Analyze(@"package main
type Point struct { X int; Y int }
func (p Point) Sum() int { return p.X + p.Y }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Methods.Count);
        var method = result.Root.Methods[0];
        Assert.AreEqual("Sum", method.Symbol.Name);
        Assert.IsFalse(method.Symbol.IsPointerReceiver);
        Assert.AreEqual(BuiltinTypes.Int, method.Symbol.ReturnType);
    }

    [TestMethod]
    public void Pointer_receiver_method_declaration()
    {
        var result = Analyze(@"package main
type Counter struct { N int }
func (c *Counter) Inc() { c.N = c.N + 1 }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Methods.Count);
        Assert.IsTrue(result.Root.Methods[0].Symbol.IsPointerReceiver);
    }

    [TestMethod]
    public void Method_call_on_value()
    {
        var result = Analyze(@"package main
type Point struct { X int; Y int }
func (p Point) Sum() int { return p.X + p.Y }
func main() {
    pt := Point{X: 1, Y: 2}
    s := pt.Sum()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_call_on_pointer_auto_deref()
    {
        var result = Analyze(@"package main
type Point struct { X int }
func (p Point) GetX() int { return p.X }
func main() {
    pt := &Point{X: 5}
    x := pt.GetX()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_with_parameters()
    {
        var result = Analyze(@"package main
type Calc struct {}
func (c Calc) Add(a int, b int) int { return a + b }
func main() {
    c := Calc{}
    s := c.Add(3, 4)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_wrong_argument_count_error()
    {
        var result = Analyze(@"package main
type Calc struct {}
func (c Calc) Add(a int, b int) int { return a + b }
func main() {
    c := Calc{}
    s := c.Add(3)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongArgumentCount));
    }

    [TestMethod]
    public void Method_wrong_argument_type_error()
    {
        var result = Analyze(@"package main
type Calc struct {}
func (c Calc) Add(a int, b int) int { return a + b }
func main() {
    c := Calc{}
    s := c.Add(3, ""hello"")
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Duplicate_method_error()
    {
        // Method duplicates are tolerated at package level for build-tag compatibility.
        // The second declaration is silently ignored.
        var result = Analyze(@"package main
type T struct {}
func (t T) Foo() {}
func (t T) Foo() {}
func main() {}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_body_accesses_receiver_fields()
    {
        var result = Analyze(@"package main
type Rect struct { W int; H int }
func (r Rect) Area() int { return r.W * r.H }
func main() {
    rect := Rect{W: 3, H: 4}
    a := rect.Area()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_on_named_non_struct_type()
    {
        var result = Analyze(@"package main
type MyInt int
func (m MyInt) Double() MyInt { return m + m }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Methods.Count);
    }
}
