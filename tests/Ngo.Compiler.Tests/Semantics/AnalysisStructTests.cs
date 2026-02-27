// -----------------------------------------------------------------------
// <copyright file="AnalysisStructTests.cs" company="Ziad">
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
public class AnalysisStructTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Bind_struct_type_declaration()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Types.Count);
        var typeDecl = result.Root.Types[0];
        Assert.IsInstanceOfType<StructTypeSymbol>(typeDecl.Symbol);
        var structType = (StructTypeSymbol)typeDecl.Symbol;
        Assert.AreEqual("Point", structType.Name);
        Assert.AreEqual(2, structType.Fields.Count);
        Assert.AreEqual("X", structType.Fields[0].Name);
        Assert.AreEqual("Y", structType.Fields[1].Name);
        Assert.AreEqual(0, structType.Fields[0].Ordinal);
        Assert.AreEqual(1, structType.Fields[1].Ordinal);
    }

    [TestMethod]
    public void Bind_struct_multiple_fields_per_line()
    {
        var result = Analyze(@"package main
type Point struct {
    X, Y int
}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.IsInstanceOfType<StructTypeSymbol>(result.Root.Types[0].Symbol);
        var structType = (StructTypeSymbol)result.Root.Types[0].Symbol;
        Assert.AreEqual(2, structType.Fields.Count);
        Assert.AreEqual("X", structType.Fields[0].Name);
        Assert.AreEqual("Y", structType.Fields[1].Name);
        Assert.AreEqual(BuiltinTypes.Int, structType.Fields[0].Type);
        Assert.AreEqual(BuiltinTypes.Int, structType.Fields[1].Type);
    }

    [TestMethod]
    public void Bind_empty_struct()
    {
        var result = Analyze(@"package main
type Empty struct {}
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.IsInstanceOfType<StructTypeSymbol>(result.Root.Types[0].Symbol);
        var structType = (StructTypeSymbol)result.Root.Types[0].Symbol;
        Assert.AreEqual(0, structType.Fields.Count);
    }

    [TestMethod]
    public void Bind_duplicate_type_declaration_reports_error()
    {
        var result = Analyze(@"package main
type Point struct { X int }
type Point struct { Y int }
func main() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.AlreadyDeclared));
    }

    [TestMethod]
    public void Bind_type_alias()
    {
        var result = Analyze(@"package main
type MyInt = int
func f(x MyInt) MyInt {
    return x
}");
        Assert.IsFalse(result.HasErrors);
        var typeDecl = result.Root.Types[0];
        Assert.AreEqual("MyInt", typeDecl.Symbol.Name);
    }

    [TestMethod]
    public void Bind_keyed_struct_literal()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{X: 1, Y: 2}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.AreEqual(2, lit.Initializers.Count);
        Assert.AreEqual("X", lit.Initializers[0].Field.Name);
        Assert.AreEqual("Y", lit.Initializers[1].Field.Name);
    }

    [TestMethod]
    public void Bind_positional_struct_literal()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{1, 2}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.AreEqual(2, lit.Initializers.Count);
        Assert.AreEqual("X", lit.Initializers[0].Field.Name);
        Assert.AreEqual("Y", lit.Initializers[1].Field.Name);
    }

    [TestMethod]
    public void Bind_empty_struct_literal()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.AreEqual(0, lit.Initializers.Count);
    }

    [TestMethod]
    public void Bind_struct_literal_wrong_field_name_reports_error()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{Z: 1}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndefinedField));
    }

    [TestMethod]
    public void Bind_struct_literal_wrong_field_type_reports_error()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{X: ""hello"", Y: 2}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_positional_struct_literal_too_few_values_reports_error()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{1}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidCompositeLiteral));
    }

    [TestMethod]
    public void Bind_positional_struct_literal_too_many_values_reports_error()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{1, 2, 3}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidCompositeLiteral));
    }

    [TestMethod]
    public void Bind_field_access()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{X: 1, Y: 2}
    x := p.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.IsInstanceOfType<SelectorExpression>(varDecl.Initializer);
        var selector = (SelectorExpression)varDecl.Initializer;
        Assert.AreEqual("X", selector.Field.Name);
        Assert.AreEqual(BuiltinTypes.Int, selector.Type);
    }

    [TestMethod]
    public void Bind_field_access_result_type()
    {
        var result = Analyze(@"package main
type Pair struct {
    Name string
    Value int
}
func main() {
    p := Pair{Name: ""hello"", Value: 42}
    s := p.Name
    v := p.Value
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var nameDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.AreEqual(BuiltinTypes.String, nameDecl.Symbol.Type);
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[2]);
        var valueDecl = (VarDeclaration)fn.Body.Statements[2];
        Assert.AreEqual(BuiltinTypes.Int, valueDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_undefined_field_reports_error()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    p := Point{X: 1, Y: 2}
    z := p.Z
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndefinedField));
    }

    [TestMethod]
    public void Bind_selector_on_non_struct_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    y := x.Foo
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidSelector));
    }

    [TestMethod]
    public void Bind_struct_as_parameter_type()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func getX(p Point) int {
    return p.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<StructTypeSymbol>(param.Type);
    }

    [TestMethod]
    public void Bind_struct_as_return_type()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func origin() Point {
    return Point{X: 0, Y: 0}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<StructTypeSymbol>(fn.Symbol.ReturnType);
    }

    [TestMethod]
    public void Bind_struct_var_with_literal_initializer()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
func main() {
    var p Point = Point{X: 1, Y: 2}
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_nested_struct()
    {
        var result = Analyze(@"package main
type Point struct {
    X int
    Y int
}
type Line struct {
    Start Point
    End Point
}
func main() {
    l := Line{Start: Point{X: 0, Y: 0}, End: Point{X: 1, Y: 1}}
    sx := l.Start.X
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.AreEqual(BuiltinTypes.Int, varDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_struct_forward_reference_in_function_signature()
    {
        var result = Analyze(@"package main
func makePoint() Point {
    return Point{X: 1, Y: 2}
}
type Point struct {
    X int
    Y int
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<StructTypeSymbol>(fn.Symbol.ReturnType);
    }
}
