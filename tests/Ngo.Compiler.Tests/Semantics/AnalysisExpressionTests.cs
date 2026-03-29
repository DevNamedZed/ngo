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

using System.IO;
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
    private static readonly string TestProjectRoot = Path.Combine(Path.GetTempPath(), "ngo-test-project");

    static AnalysisExpressionTests()
    {
        Directory.CreateDirectory(TestProjectRoot);
    }

    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
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

    [TestMethod]
    public void Method_with_unnamed_receiver_resolves()
    {
        // Go allows methods where the receiver has no variable name, just the type
        // e.g. func (_loadUndef) exec(vm *vm) { ... }
        var result = Analyze(@"package main
type myInstr struct{}
type vm struct{ pc int }
func (myInstr) exec(v *vm) { v.pc++ }
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void Method_on_named_int_type_resolves()
    {
        // type valueInt int64 with methods — like goja pattern
        var result = Analyze(@"package main
type valueInt int64
func (v valueInt) ToInt() int { return int(v) }
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void Method_on_named_type_across_files()
    {
        // Simulates goja: type defined in file1, methods in file2
        var tree1 = SyntaxTree.Parse(@"package main
type valueInt int64
");
        var tree2 = SyntaxTree.Parse(@"package main
func (v valueInt) ToInt() int { return int(v) }
");
        var result = SemanticAnalyzer.Analyze(new[] { tree1, tree2 }, new CompilationContext(TestProjectRoot));
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Func_type_conversion_does_not_derail_parser()
    {
        // (func(T) R)(nil) is a type conversion, not a function literal.
        // The parser must not consume subsequent declarations as part of a function body.
        var tree = SyntaxTree.Parse(@"package main
var x = (func(int) int)(nil)
type Foo struct{}
");
        var types = tree.Root.Members.OfType<Ngo.Compiler.Language.Syntax.TypeDeclarationSyntax>()
            .SelectMany(t => t.Specs).Select(s => s.Name.Text).ToList();
        CollectionAssert.Contains(types, "Foo", $"Types: [{string.Join(", ", types)}]");
    }

    [TestMethod]
    public void Embedded_generic_field_promoted_access()
    {
        // Embedded generic type fields should be promoted
        var result = Analyze(@"package main
type inner[T any] struct { val T }
type outer[T any] struct { inner[T] }
func f() int {
    var o outer[int]
    return o.val
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void Range_over_named_map_type()
    {
        // Named types with underlying map type should be rangeable
        var result = Analyze(@"package main
type StringMap map[string]string
func f() {
    var m StringMap
    for k, v := range m {
        _ = k
        _ = v
    }
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void Range_over_named_slice_type()
    {
        // Named types with underlying slice type should be rangeable
        var result = Analyze(@"package main
type IntSlice []int
func f() {
    var s IntSlice
    for i, v := range s {
        _ = i
        _ = v
    }
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public void RawMessage_assignable_to_byte_slice()
    {
        // json.RawMessage (type RawMessage []byte) should be assignable to []byte
        var tree = SyntaxTree.Parse(@"package main
import ""encoding/json""
func f() {
    var rm json.RawMessage
    var b []byte = rm
    _ = b
}
");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Json_Indent_function_resolves()
    {
        var tree = SyntaxTree.Parse(@"package main
import ""encoding/json""
func f() {
    _ = json.Indent
}
");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
        // Just ensure json.Indent is a known export (not UndeclaredName)
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("Indent")),
            string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Base64_NewEncoding_function_resolves()
    {
        var tree = SyntaxTree.Parse(@"package main
import ""encoding/base64""
func f() {
    _ = base64.NewEncoding
}
");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("NewEncoding")),
            string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Os_DirFS_function_resolves()
    {
        var tree = SyntaxTree.Parse(@"package main
import ""os""
func f() {
    _ = os.DirFS
}
");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("DirFS")),
            string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Binary_Size_function_resolves()
    {
        var tree = SyntaxTree.Parse(@"package main
import ""encoding/binary""
func f() {
    _ = binary.Size
}
");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(TestProjectRoot));
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("Size")),
            string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Send_on_named_channel_type()
    {
        // type control chan bool; should support ch <- true
        var result = Analyze(@"package main
type control chan bool
func f() {
    var ch control
    ch <- true
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Receive_from_named_channel_type()
    {
        // type control chan int; should support <-ch
        var result = Analyze(@"package main
type events chan int
func f() {
    var ch events
    v := <-ch
    _ = v
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Named_type_assignable_to_underlying()
    {
        // type MyInt int; var x int = MyInt(5) should work
        var result = Analyze(@"package main
type MySlice []int
func f() {
    var s MySlice
    var raw []int = s
    _ = raw
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Builtin_close_shadowed_by_parameter()
    {
        // A parameter named 'close' should shadow the builtin close()
        var result = Analyze(@"package main
func shutdown(close func(int) error) error {
    return close(42)
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Builtin_new_shadowed_by_local()
    {
        // A local variable named 'new' should shadow the builtin new()
        var result = Analyze(@"package main
func f() int {
    new := func(x int) int { return x + 1 }
    return new(5)
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Reflect_Select_function_resolves()
    {
        var result = Analyze(@"package main
import ""reflect""
func f() {
    cases := []reflect.SelectCase{}
    chosen, value, recvOK := reflect.Select(cases)
    _, _, _ = chosen, value, recvOK
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Log_Default_function_resolves()
    {
        var result = Analyze(@"package main
import ""log""
func f() {
    l := log.Default()
    _ = l
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Bytes_Clone_function_resolves()
    {
        var result = Analyze(@"package main
import ""bytes""
func f() {
    b := bytes.Clone([]byte{1, 2, 3})
    _ = b
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Http_DetectContentType_function_resolves()
    {
        var result = Analyze(@"package main
import ""net/http""
func f() {
    ct := http.DetectContentType([]byte{})
    _ = ct
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Runtime_MemStats_PauseNs_field_resolves()
    {
        var result = Analyze(@"package main
import ""runtime""
func f() {
    var m runtime.MemStats
    runtime.ReadMemStats(&m)
    _ = m.PauseNs
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Generic_struct_field_access_substitutes_type_param()
    {
        var result = Analyze(@"package main
type Box[T any] struct { Value T }
func f() int {
    var b Box[int]
    return b.Value
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        var fn = result.Root.Functions[0];
        var ret = (ReturnStatement)fn.Body.Statements[1];
        var sel = (SelectorExpression)ret.Values[0];
        Assert.AreEqual("int", sel.Type.Name);
    }

    [TestMethod]
    public void Generic_struct_nested_field_access_substitutes_type_param()
    {
        var result = Analyze(@"package main
type Inner[T any] struct { Data T }
type Outer[T any] struct { Inner Inner[T] }
func f() string {
    var o Outer[string]
    return o.Inner.Data
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [TestMethod]
    public void Unsafe_Pointer_accepts_nil()
    {
        var result = Analyze(@"package main
import ""unsafe""
func f() unsafe.Pointer {
    return nil
}
");
        Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }
}
