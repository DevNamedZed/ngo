// -----------------------------------------------------------------------
// <copyright file="AnalysisPointerTests.cs" company="Ziad">
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
public class AnalysisPointerTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
    }

    [TestMethod]
    public void Bind_pointer_type_parameter()
    {
        var result = Analyze(@"package main
func f(p *int) int {
    return *p
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<PointerTypeSymbol>(param.Type);
        var ptrType = (PointerTypeSymbol)param.Type;
        Assert.AreEqual(BuiltinTypes.Int, ptrType.ElementType);
    }

    [TestMethod]
    public void Bind_pointer_to_struct_parameter()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func f(p *Point) int {
    return p.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<PointerTypeSymbol>(param.Type);
        var ptrType = (PointerTypeSymbol)param.Type;
        Assert.IsInstanceOfType<StructTypeSymbol>(ptrType.ElementType);
    }

    [TestMethod]
    public void Bind_address_of_variable()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    p := &x
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.IsInstanceOfType<AddressOfExpression>(varDecl.Initializer);
        var addrOf = (AddressOfExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<PointerTypeSymbol>(addrOf.Type);
        var ptrType = (PointerTypeSymbol)addrOf.Type;
        Assert.AreEqual(BuiltinTypes.Int, ptrType.ElementType);
    }

    [TestMethod]
    public void Bind_address_of_field()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{X: 1, Y: 2}
    px := &p.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.IsInstanceOfType<AddressOfExpression>(varDecl.Initializer);
        var addrOf = (AddressOfExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<PointerTypeSymbol>(addrOf.Type);
        var ptrType = (PointerTypeSymbol)addrOf.Type;
        Assert.AreEqual(BuiltinTypes.Int, ptrType.ElementType);
    }

    [TestMethod]
    public void Bind_address_of_non_addressable_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    p := &42
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidAddressOf));
    }

    [TestMethod]
    public void Bind_deref_pointer()
    {
        var result = Analyze(@"package main
func f(p *int) int {
    return *p
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<DerefExpression>(ret.Value);
        var deref = (DerefExpression)ret.Value;
        Assert.AreEqual(BuiltinTypes.Int, deref.Type);
    }

    [TestMethod]
    public void Bind_deref_non_pointer_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    y := *x
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_pointer_auto_deref_on_field_access()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func f(p *Point) int {
    return p.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<SelectorExpression>(ret.Value);
        var selector = (SelectorExpression)ret.Value;
        Assert.AreEqual("X", selector.Field.Name);
        Assert.AreEqual(BuiltinTypes.Int, selector.Type);
        // Target should be auto-deref'd
        Assert.IsInstanceOfType<DerefExpression>(selector.Target);
    }

    [TestMethod]
    public void Bind_nil_assignable_to_pointer()
    {
        var result = Analyze(@"package main
func main() {
    var p *int = nil
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_nil_not_assignable_to_int_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    var x int = nil
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_pointer_return_type()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func newPoint() *Point {
    p := Point{X: 0, Y: 0}
    return &p
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<PointerTypeSymbol>(fn.Symbol.ReturnType);
        var ptrType = (PointerTypeSymbol)fn.Symbol.ReturnType;
        Assert.IsInstanceOfType<StructTypeSymbol>(ptrType.ElementType);
    }

    [TestMethod]
    public void Bind_assign_nil_to_pointer_variable()
    {
        var result = Analyze(@"package main
func main() {
    var p *int = nil
    p = nil
}");
        Assert.IsFalse(result.HasErrors);
    }
}
