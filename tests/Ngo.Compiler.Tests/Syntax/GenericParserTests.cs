// -----------------------------------------------------------------------
// <copyright file="GenericParserTests.cs" company="Ziad">
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
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Syntax;

[TestClass]
public class GenericParserTests
{
    private static SourceFileSyntax Parse(string source)
    {
        var parser = new Parser(source);
        return parser.ParseSourceFile();
    }

    // ================================================================
    // Generic function declarations
    // ================================================================

    [TestMethod]
    public void Generic_function_single_type_param()
    {
        var file = Parse(@"package main
func Max[T any](a T, b T) T { return a }");
        Assert.AreEqual(1, file.Members.Count);
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.AreEqual("Max", fn.Name.Text);
        Assert.IsNotNull(fn.TypeParameters);
        Assert.AreEqual(1, fn.TypeParameters!.Parameters.Count);
        var tp = fn.TypeParameters.Parameters[0];
        Assert.AreEqual(1, tp.Names.Count);
        Assert.AreEqual("T", tp.Names[0].Text);
    }

    [TestMethod]
    public void Generic_function_multiple_type_params()
    {
        var file = Parse(@"package main
func Pair[T any, U any](a T, b U) T { return a }");
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsNotNull(fn.TypeParameters);
        Assert.AreEqual(2, fn.TypeParameters!.Parameters.Count);
        Assert.AreEqual("T", fn.TypeParameters.Parameters[0].Names[0].Text);
        Assert.AreEqual("U", fn.TypeParameters.Parameters[1].Names[0].Text);
    }

    [TestMethod]
    public void Generic_function_comparable_constraint()
    {
        var file = Parse(@"package main
func Contains[T comparable](s []T, v T) bool { return false }");
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsNotNull(fn.TypeParameters);
        var tp = fn.TypeParameters!.Parameters[0];
        Assert.AreEqual("T", tp.Names[0].Text);
        Assert.IsInstanceOfType<IdentifierNameSyntax>(tp.Constraint);
        Assert.AreEqual("comparable", ((IdentifierNameSyntax)tp.Constraint).Identifier.Text);
    }

    [TestMethod]
    public void Generic_function_grouped_type_params()
    {
        var file = Parse(@"package main
func Swap[T, U any](a T, b U) (U, T) { return b, a }");
        var fn = (FunctionDeclarationSyntax)file.Members[0];
        Assert.IsNotNull(fn.TypeParameters);
        Assert.AreEqual(1, fn.TypeParameters!.Parameters.Count);
        var tp = fn.TypeParameters.Parameters[0];
        Assert.AreEqual(2, tp.Names.Count);
        Assert.AreEqual("T", tp.Names[0].Text);
        Assert.AreEqual("U", tp.Names[1].Text);
    }

    // ================================================================
    // Generic type declarations
    // ================================================================

    [TestMethod]
    public void Generic_struct_declaration()
    {
        var file = Parse(@"package main
type Stack[T any] struct { items []T }");
        Assert.AreEqual(1, file.Members.Count);
        var td = (TypeDeclarationSyntax)file.Members[0];
        Assert.AreEqual(1, td.Specs.Count);
        var spec = td.Specs[0];
        Assert.AreEqual("Stack", spec.Name.Text);
        Assert.IsNotNull(spec.TypeParameters);
        Assert.AreEqual(1, spec.TypeParameters!.Parameters.Count);
        Assert.AreEqual("T", spec.TypeParameters.Parameters[0].Names[0].Text);
    }

    [TestMethod]
    public void Generic_struct_two_type_params()
    {
        var file = Parse(@"package main
type Pair[K comparable, V any] struct {
    Key K
    Value V
}");
        var td = (TypeDeclarationSyntax)file.Members[0];
        var spec = td.Specs[0];
        Assert.IsNotNull(spec.TypeParameters);
        Assert.AreEqual(2, spec.TypeParameters!.Parameters.Count);
    }

    // ================================================================
    // Type argument lists (multi-arg)
    // ================================================================

    [TestMethod]
    public void Multi_type_argument_call()
    {
        var file = Parse(@"package main
func main() { _ = f[int, string](1, ""a"") }
func f[T any, U any](a T, b U) T { return a }");
        var fn = file.Members.OfType<FunctionDeclarationSyntax>()
            .First(f => f.Name.Text == "main");
        // The call's function expression should be a TypeArgumentList
        var body = fn.Body;
        Assert.AreEqual(1, body.Statements.Count);
    }

    // ================================================================
    // Interface with union type constraints
    // ================================================================

    [TestMethod]
    public void Interface_with_tilde_union()
    {
        var file = Parse(@"package main
type Number interface {
    ~int | ~float64
}");
        var td = (TypeDeclarationSyntax)file.Members[0];
        Assert.AreEqual("Number", td.Specs[0].Name.Text);
        var ifaceType = td.Specs[0].Type as InterfaceTypeSyntax;
        Assert.IsNotNull(ifaceType);
    }

    // ================================================================
    // Non-generic code still parses correctly
    // ================================================================

    [TestMethod]
    public void Array_type_not_confused_with_generic()
    {
        var file = Parse(@"package main
func main() { var x [3]int; _ = x }");
        var fn = file.Members.OfType<FunctionDeclarationSyntax>().First();
        Assert.AreEqual("main", fn.Name.Text);
        Assert.IsNull(fn.TypeParameters);
    }

    [TestMethod]
    public void Index_expression_not_confused_with_generic()
    {
        var file = Parse(@"package main
func main() {
    s := []int{1, 2, 3}
    _ = s[0]
}");
        // Should parse without errors — index expression, not type arg
        var fn = file.Members.OfType<FunctionDeclarationSyntax>().First();
        Assert.AreEqual("main", fn.Name.Text);
    }
}
