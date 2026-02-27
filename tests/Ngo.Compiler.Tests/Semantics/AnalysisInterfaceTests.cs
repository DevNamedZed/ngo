// -----------------------------------------------------------------------
// <copyright file="AnalysisInterfaceTests.cs" company="Ziad">
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
public class AnalysisInterfaceTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Interface_type_declaration_single_method()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Types.Count);
        Assert.IsInstanceOfType<InterfaceTypeSymbol>(result.Root.Types[0].Symbol);
        var ifaceType = (InterfaceTypeSymbol)result.Root.Types[0].Symbol;
        Assert.AreEqual("Stringer", ifaceType.Name);
        Assert.AreEqual(1, ifaceType.Methods.Count);
        Assert.AreEqual("String", ifaceType.Methods[0].Name);
        Assert.AreEqual(BuiltinTypes.String, ifaceType.Methods[0].ReturnType);
    }

    [TestMethod]
    public void Interface_type_declaration_multiple_methods()
    {
        var result = Analyze(@"package main
type ReadWriter interface {
    Read(buf []byte) int
    Write(buf []byte) int
}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.IsInstanceOfType<InterfaceTypeSymbol>(result.Root.Types[0].Symbol);
        var ifaceType = (InterfaceTypeSymbol)result.Root.Types[0].Symbol;
        Assert.AreEqual(2, ifaceType.Methods.Count);
        Assert.AreEqual("Read", ifaceType.Methods[0].Name);
        Assert.AreEqual("Write", ifaceType.Methods[1].Name);
    }

    [TestMethod]
    public void Empty_interface_declaration()
    {
        var result = Analyze(@"package main
type Any interface {}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.IsInstanceOfType<InterfaceTypeSymbol>(result.Root.Types[0].Symbol);
        var ifaceType = (InterfaceTypeSymbol)result.Root.Types[0].Symbol;
        Assert.AreEqual(0, ifaceType.Methods.Count);
    }

    [TestMethod]
    public void Struct_satisfies_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func (p Point) String() string { return ""point"" }
func greet(s Stringer) {}
func main() {
    p := Point{X: 1}
    greet(p)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Struct_missing_method_does_not_satisfy_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func greet(s Stringer) {}
func main() {
    p := Point{X: 1}
    greet(p)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Nil_assignable_to_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
func main() {
    var s Stringer = nil
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Empty_interface_accepts_any_type()
    {
        var result = Analyze(@"package main
type Any interface {}
func take(a Any) {}
func main() {
    take(42)
    take(""hello"")
    take(true)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Interface_variable_assignment()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Name struct {}
func (n Name) String() string { return ""name"" }
func main() {
    var s Stringer = Name{}
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Interface_method_call()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Name struct {}
func (n Name) String() string { return ""name"" }
func main() {
    var s Stringer = Name{}
    x := s.String()
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Pointer_satisfies_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func (p Point) String() string { return ""point"" }
func greet(s Stringer) {}
func main() {
    p := &Point{X: 1}
    greet(p)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_assert_on_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Name struct {}
func (n Name) String() string { return ""name"" }
func main() {
    var s Stringer = Name{}
    n := s.(Name)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Type_assert_on_non_interface_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    y := x.(int)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidTypeAssert));
    }

    // --- Method set computation tests ---

    [TestMethod]
    public void Value_type_with_pointer_receiver_does_not_satisfy_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func (p *Point) String() string { return ""point"" }
func greet(s Stringer) {}
func main() {
    p := Point{X: 1}
    greet(p)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Pointer_type_with_pointer_receiver_satisfies_interface()
    {
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func (p *Point) String() string { return ""point"" }
func greet(s Stringer) {}
func main() {
    p := &Point{X: 1}
    greet(p)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Pointer_type_with_value_receiver_satisfies_interface()
    {
        // *T includes value-receiver methods in its method set
        var result = Analyze(@"package main
type Stringer interface { String() string }
type Point struct { X int }
func (p Point) String() string { return ""point"" }
func greet(s Stringer) {}
func main() {
    p := &Point{X: 1}
    greet(p)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Mixed_receivers_pointer_satisfies_but_value_does_not()
    {
        // Interface requires two methods: one has value receiver, one has pointer receiver.
        // Only *T satisfies, not T.
        var result = Analyze(@"package main
type Worker interface {
    Name() string
    Run()
}
type Task struct {}
func (t Task) Name() string { return ""task"" }
func (t *Task) Run() {}
func doWork(w Worker) {}
func main() {
    t := &Task{}
    doWork(t)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Mixed_receivers_value_type_fails_interface()
    {
        var result = Analyze(@"package main
type Worker interface {
    Name() string
    Run()
}
type Task struct {}
func (t Task) Name() string { return ""task"" }
func (t *Task) Run() {}
func doWork(w Worker) {}
func main() {
    var t Task
    doWork(t)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    // --- Struct embedding interface satisfaction tests ---

    [TestMethod]
    public void Embedded_struct_promotes_method_to_satisfy_interface()
    {
        var result = Analyze(@"package main
type Speaker interface { Speak() string }
type Animal struct { Name string }
func (a Animal) Speak() string { return a.Name }
type Dog struct { Animal }
func greet(s Speaker) {}
func main() {
    d := Dog{Animal: Animal{Name: ""Rex""}}
    greet(d)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Embedded_struct_missing_method_does_not_satisfy_interface()
    {
        var result = Analyze(@"package main
type Speaker interface { Speak() string }
type Base struct {}
type Derived struct { Base }
func greet(s Speaker) {}
func main() {
    d := Derived{}
    greet(d)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Direct_method_shadows_promoted_method_for_interface()
    {
        var result = Analyze(@"package main
type Speaker interface { Speak() string }
type Animal struct {}
func (a Animal) Speak() string { return ""animal"" }
type Dog struct { Animal }
func (d Dog) Speak() string { return ""dog"" }
func greet(s Speaker) {}
func main() {
    greet(Dog{})
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Pointer_to_embedding_struct_satisfies_interface()
    {
        var result = Analyze(@"package main
type Speaker interface { Speak() string }
type Animal struct {}
func (a Animal) Speak() string { return ""animal"" }
type Dog struct { Animal }
func greet(s Speaker) {}
func main() {
    d := &Dog{}
    greet(d)
}");
        Assert.IsFalse(result.HasErrors);
    }
}
