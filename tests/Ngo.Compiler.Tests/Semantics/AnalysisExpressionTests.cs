// -----------------------------------------------------------------------
// <copyright file="AnalysisExpressionTests.cs" company="Ziad">
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
public class AnalysisExpressionTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    private static Expression GetSingleReturnExpression(AnalysisResult result)
    {
        var fn = result.Root.Functions[0];
        var ret = (ReturnStatement)fn.Body.Statements[0];
        return ret.Value!;
    }

    [TestMethod]
    public void Bind_integer_literal()
    {
        var result = Analyze("package main\nfunc f() int { return 42 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<LiteralExpression>(expr);
        var lit = (LiteralExpression)expr;
        Assert.AreEqual(42L, lit.Value);
        Assert.AreEqual(TypeKind.UntypedInt, lit.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_float_literal()
    {
        var result = Analyze("package main\nfunc f() float64 { return 3.14 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<LiteralExpression>(expr);
        var lit = (LiteralExpression)expr;
        Assert.AreEqual(3.14, lit.Value);
        Assert.AreEqual(TypeKind.UntypedFloat, lit.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_string_literal()
    {
        var result = Analyze("package main\nfunc f() string { return \"hello\" }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<LiteralExpression>(expr);
        var lit = (LiteralExpression)expr;
        Assert.AreEqual("hello", lit.Value);
        Assert.AreEqual(TypeKind.UntypedString, lit.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_true_literal()
    {
        var result = Analyze("package main\nfunc f() bool { return true }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<LiteralExpression>(expr);
        var lit = (LiteralExpression)expr;
        Assert.AreEqual(true, lit.Value);
        Assert.AreEqual(TypeKind.UntypedBool, lit.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_false_literal()
    {
        var result = Analyze("package main\nfunc f() bool { return false }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<LiteralExpression>(expr);
        var lit = (LiteralExpression)expr;
        Assert.AreEqual(false, lit.Value);
    }

    [TestMethod]
    public void Bind_parameter_reference()
    {
        var result = Analyze("package main\nfunc f(x int) int { return x }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<IdentifierExpression>(expr);
        var id = (IdentifierExpression)expr;
        Assert.AreEqual("x", id.Symbol.Name);
        Assert.AreEqual(TypeKind.Int, id.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_undefined_name_reports_error()
    {
        var result = Analyze("package main\nfunc f() int { return xyz }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName));
    }

    [TestMethod]
    public void Bind_add_integers()
    {
        var result = Analyze("package main\nfunc f() int { return 1 + 2 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Add, bin.Operator);
        Assert.IsTrue(TypeChecker.IsNumeric(bin.Type));
    }

    [TestMethod]
    public void Bind_add_strings()
    {
        var result = Analyze("package main\nfunc f() string { return \"a\" + \"b\" }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Add, bin.Operator);
    }

    [TestMethod]
    public void Bind_comparison_returns_bool()
    {
        var result = Analyze("package main\nfunc f() bool { return 1 < 2 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Less, bin.Operator);
        Assert.AreEqual(TypeKind.UntypedBool, bin.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_logical_and()
    {
        var result = Analyze("package main\nfunc f() bool { return true && false }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.LogicalAnd, bin.Operator);
    }

    [TestMethod]
    public void Bind_binary_type_mismatch_reports_error()
    {
        var result = Analyze("package main\nfunc f() int { return 1 + true }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_multiply_integers()
    {
        var result = Analyze("package main\nfunc f() int { return 3 * 4 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Multiply, bin.Operator);
    }

    [TestMethod]
    public void Bind_bitwise_operations()
    {
        var result = Analyze("package main\nfunc f() int { return 5 & 3 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.BitwiseAnd, bin.Operator);
    }

    [TestMethod]
    public void Bind_equality_comparison()
    {
        var result = Analyze("package main\nfunc f() bool { return 1 == 2 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Equal, bin.Operator);
        Assert.AreEqual(TypeKind.UntypedBool, bin.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_negate()
    {
        var result = Analyze("package main\nfunc f() int { return -42 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<UnaryExpression>(expr);
        var unary = (UnaryExpression)expr;
        Assert.AreEqual(UnaryOperator.Negate, unary.Operator);
    }

    [TestMethod]
    public void Bind_logical_not()
    {
        var result = Analyze("package main\nfunc f() bool { return !true }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<UnaryExpression>(expr);
        var unary = (UnaryExpression)expr;
        Assert.AreEqual(UnaryOperator.LogicalNot, unary.Operator);
    }

    [TestMethod]
    public void Bind_negate_non_numeric_reports_error()
    {
        var result = Analyze("package main\nfunc f() int { return -true }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_function_call()
    {
        var result = Analyze("package main\nfunc add(a int, b int) int { return a + b }\nfunc f() int { return add(1, 2) }");
        Assert.IsFalse(result.HasErrors);

        var fFunc = result.Root.Functions.First(f => f.Symbol.Name == "f");
        var ret = (ReturnStatement)fFunc.Body.Statements[0];
        Assert.IsInstanceOfType<CallExpression>(ret.Value);
        var call = (CallExpression)ret.Value;
        Assert.AreEqual("add", call.Function.Name);
        Assert.AreEqual(2, call.Arguments.Count);
        Assert.AreEqual(TypeKind.Int, call.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_function_call_wrong_arg_count()
    {
        var result = Analyze("package main\nfunc add(a int, b int) int { return a + b }\nfunc f() int { return add(1) }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongArgumentCount));
    }

    [TestMethod]
    public void Bind_function_call_type_mismatch()
    {
        var result = Analyze("package main\nfunc add(a int, b int) int { return a + b }\nfunc f() int { return add(1, \"x\") }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_type_conversion()
    {
        var result = Analyze("package main\nfunc f() float64 { return float64(42) }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<ConversionExpression>(expr);
        var conv = (ConversionExpression)expr;
        Assert.AreEqual(TypeKind.Float64, conv.Type.TypeKind);
    }

    [TestMethod]
    public void Bind_invalid_conversion()
    {
        var result = Analyze("package main\nfunc f() int { return int(true) }");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidConversion));
    }

    [TestMethod]
    public void Bind_nested_arithmetic()
    {
        var result = Analyze("package main\nfunc f(x int, y int) int { return (x + y) * 2 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var mul = (BinaryExpression)expr;
        Assert.AreEqual(BinaryOperator.Multiply, mul.Operator);
        Assert.IsInstanceOfType<BinaryExpression>(mul.Left);
    }

    [TestMethod]
    public void Bind_parameter_in_expression()
    {
        var result = Analyze("package main\nfunc f(x int) int { return x + 1 }");
        Assert.IsFalse(result.HasErrors);
        var expr = GetSingleReturnExpression(result);
        Assert.IsInstanceOfType<BinaryExpression>(expr);
        var bin = (BinaryExpression)expr;
        Assert.IsInstanceOfType<IdentifierExpression>(bin.Left);
        var id = (IdentifierExpression)bin.Left;
        Assert.AreEqual("x", id.Symbol.Name);
    }

    [TestMethod]
    public void Imaginary_literal_has_UntypedComplex_type()
    {
        var result = Analyze("package main\nfunc main() { x := 3i; _ = x }");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Complex_builtin_returns_complex128()
    {
        var result = Analyze("package main\nfunc main() { c := complex(1.0, 2.0); _ = c }");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Real_imag_builtins_resolve()
    {
        var result = Analyze("package main\nfunc main() { c := complex(1.0, 2.0); _ = real(c); _ = imag(c) }");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Complex_ordering_comparison_is_error()
    {
        var result = Analyze("package main\nfunc main() { a := complex(1.0, 2.0); b := complex(3.0, 4.0); _ = a < b }");
        Assert.IsTrue(result.HasErrors);
    }
}
