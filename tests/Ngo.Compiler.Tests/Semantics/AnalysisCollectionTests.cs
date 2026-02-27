// -----------------------------------------------------------------------
// <copyright file="AnalysisCollectionTests.cs" company="Ziad">
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
public class AnalysisCollectionTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    // ---- Type resolution ----

    [TestMethod]
    public void Resolve_slice_parameter_type()
    {
        var result = Analyze(@"package main
func f(s []int) int {
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<SliceTypeSymbol>(param.Type);
        var sliceType = (SliceTypeSymbol)param.Type;
        Assert.AreEqual(BuiltinTypes.Int, sliceType.ElementType);
    }

    [TestMethod]
    public void Resolve_array_parameter_type()
    {
        var result = Analyze(@"package main
func f(a [3]int) int {
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<ArrayTypeSymbol>(param.Type);
        var arrayType = (ArrayTypeSymbol)param.Type;
        Assert.AreEqual(BuiltinTypes.Int, arrayType.ElementType);
        Assert.AreEqual(3, arrayType.Length);
    }

    [TestMethod]
    public void Resolve_map_parameter_type()
    {
        var result = Analyze(@"package main
func f(m map[string]int) int {
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<MapTypeSymbol>(param.Type);
        var mapType = (MapTypeSymbol)param.Type;
        Assert.AreEqual(BuiltinTypes.String, mapType.KeyType);
        Assert.AreEqual(BuiltinTypes.Int, mapType.ValueType);
    }

    [TestMethod]
    public void Resolve_nested_slice_of_slices()
    {
        var result = Analyze(@"package main
func f(s [][]int) int {
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        var param = fn.Symbol.Parameters[0];
        Assert.IsInstanceOfType<SliceTypeSymbol>(param.Type);
        var outerSlice = (SliceTypeSymbol)param.Type;
        Assert.IsInstanceOfType<SliceTypeSymbol>(outerSlice.ElementType);
        var innerSlice = (SliceTypeSymbol)outerSlice.ElementType;
        Assert.AreEqual(BuiltinTypes.Int, innerSlice.ElementType);
    }

    // ---- Composite literals ----

    [TestMethod]
    public void Bind_empty_slice_literal()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<SliceTypeSymbol>(lit.Type);
        Assert.IsNotNull(lit.Elements);
        Assert.AreEqual(0, lit.Elements.Count);
    }

    [TestMethod]
    public void Bind_slice_literal_with_elements()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<SliceTypeSymbol>(lit.Type);
        var sliceType = (SliceTypeSymbol)lit.Type;
        Assert.AreEqual(BuiltinTypes.Int, sliceType.ElementType);
        Assert.AreEqual(3, lit.Elements!.Count);
    }

    [TestMethod]
    public void Bind_slice_literal_type_mismatch_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{""hello""}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_array_literal_with_length()
    {
        var result = Analyze(@"package main
func main() {
    a := [3]int{1, 2, 3}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<ArrayTypeSymbol>(lit.Type);
        var arrayType = (ArrayTypeSymbol)lit.Type;
        Assert.AreEqual(3, arrayType.Length);
        Assert.AreEqual(3, lit.Elements!.Count);
    }

    [TestMethod]
    public void Bind_array_literal_wrong_count_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    a := [3]int{1, 2}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidCompositeLiteral));
    }

    [TestMethod]
    public void Bind_array_literal_with_ellipsis()
    {
        var result = Analyze(@"package main
func main() {
    a := [...]int{1, 2, 3}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<ArrayTypeSymbol>(lit.Type);
        var arrayType = (ArrayTypeSymbol)lit.Type;
        Assert.AreEqual(3, arrayType.Length);
    }

    [TestMethod]
    public void Bind_empty_map_literal()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<MapTypeSymbol>(lit.Type);
        Assert.IsNotNull(lit.Elements);
        Assert.AreEqual(0, lit.Elements.Count);
    }

    [TestMethod]
    public void Bind_map_literal_with_entries()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CompositeLiteralExpression>(varDecl.Initializer);
        var lit = (CompositeLiteralExpression)varDecl.Initializer;
        Assert.IsInstanceOfType<MapTypeSymbol>(lit.Type);
        var mapType = (MapTypeSymbol)lit.Type;
        Assert.AreEqual(2, lit.Elements!.Count);
        Assert.IsNotNull(lit.Elements[0].Key);
        Assert.IsNotNull(lit.Elements[1].Key);
    }

    [TestMethod]
    public void Bind_map_literal_non_kv_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{42}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidCompositeLiteral));
    }

    [TestMethod]
    public void Bind_map_literal_key_type_mismatch_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{42: 1}
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    // ---- Index expressions ----

    [TestMethod]
    public void Bind_slice_index_returns_element_type()
    {
        var result = Analyze(@"package main
func f(s []int) int {
    return s[0]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<IndexExpression>(ret.Value);
        var idx = (IndexExpression)ret.Value;
        Assert.AreEqual(BuiltinTypes.Int, idx.Type);
    }

    [TestMethod]
    public void Bind_array_index_returns_element_type()
    {
        var result = Analyze(@"package main
func main() {
    a := [3]int{1, 2, 3}
    x := a[0]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.AreEqual(BuiltinTypes.Int, varDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_map_index_returns_value_type()
    {
        var result = Analyze(@"package main
func f(m map[string]int) int {
    return m[""key""]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<IndexExpression>(ret.Value);
        var idx = (IndexExpression)ret.Value;
        Assert.AreEqual(BuiltinTypes.Int, idx.Type);
    }

    [TestMethod]
    public void Bind_string_index_returns_byte()
    {
        var result = Analyze(@"package main
func f(s string) byte {
    return s[0]
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_index_non_indexable_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    y := x[0]
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidIndex));
    }

    [TestMethod]
    public void Bind_index_wrong_type_reports_error()
    {
        var result = Analyze(@"package main
func f(s []int) int {
    return s[""bad""]
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidIndex));
    }

    // ---- Slice expressions ----

    [TestMethod]
    public void Bind_slice_expression_on_slice()
    {
        var result = Analyze(@"package main
func f(s []int) []int {
    return s[1:3]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<SliceExpression>(ret.Value);
        var sliceExpr = (SliceExpression)ret.Value;
        Assert.IsInstanceOfType<SliceTypeSymbol>(sliceExpr.Type);
    }

    [TestMethod]
    public void Bind_slice_expression_on_array_returns_slice()
    {
        var result = Analyze(@"package main
func main() {
    a := [3]int{1, 2, 3}
    s := a[1:3]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[1]);
        var varDecl = (VarDeclaration)fn.Body.Statements[1];
        Assert.IsInstanceOfType<SliceTypeSymbol>(varDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_slice_expression_on_string()
    {
        var result = Analyze(@"package main
func f(s string) string {
    return s[1:3]
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_slice_expression_three_index()
    {
        var result = Analyze(@"package main
func f(s []int) []int {
    return s[1:3:5]
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<SliceExpression>(ret.Value);
        var sliceExpr = (SliceExpression)ret.Value;
        Assert.IsNotNull(sliceExpr.Max);
    }

    [TestMethod]
    public void Bind_slice_expression_non_sliceable_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    x := 42
    y := x[1:3]
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidSlice));
    }

    // ---- Builtin functions ----

    [TestMethod]
    public void Bind_len_on_slice()
    {
        var result = Analyze(@"package main
func f(s []int) int {
    return len(s)
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ReturnStatement>(fn.Body.Statements[0]);
        var ret = (ReturnStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<CallExpression>(ret.Value);
        var call = (CallExpression)ret.Value;
        Assert.AreEqual("len", call.Function.Name);
    }

    [TestMethod]
    public void Bind_len_on_string()
    {
        var result = Analyze(@"package main
func f(s string) int {
    return len(s)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_len_on_map()
    {
        var result = Analyze(@"package main
func f(m map[string]int) int {
    return len(m)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_len_on_array()
    {
        var result = Analyze(@"package main
func main() {
    a := [3]int{1, 2, 3}
    n := len(a)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_len_on_int_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    n := len(42)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_cap_on_slice()
    {
        var result = Analyze(@"package main
func f(s []int) int {
    return cap(s)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_cap_on_int_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    n := cap(42)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_append_basic()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2}
    s = append(s, 3)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_append_type_mismatch_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2}
    s = append(s, ""bad"")
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_append_non_slice_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    x := append(42, 1)
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    [TestMethod]
    public void Bind_make_slice_two_args()
    {
        var result = Analyze(@"package main
func main() {
    s := make([]int, 5)
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<SliceTypeSymbol>(varDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_make_slice_three_args()
    {
        var result = Analyze(@"package main
func main() {
    s := make([]int, 5, 10)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_make_map()
    {
        var result = Analyze(@"package main
func main() {
    m := make(map[string]int)
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<VarDeclaration>(fn.Body.Statements[0]);
        var varDecl = (VarDeclaration)fn.Body.Statements[0];
        Assert.IsInstanceOfType<MapTypeSymbol>(varDecl.Symbol.Type);
    }

    [TestMethod]
    public void Bind_delete_on_map()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{""a"": 1}
    delete(m, ""a"")
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_delete_non_map_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    delete(42, ""key"")
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidOperation));
    }

    // ---- For-range ----

    [TestMethod]
    public void Bind_for_range_slice_with_kv()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
    for k, v := range s {
        x := k + v
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForRangeStatement>(fn.Body.Statements[1]);
        var forRange = (ForRangeStatement)fn.Body.Statements[1];
        Assert.IsNotNull(forRange.Key);
        Assert.IsNotNull(forRange.Value);
        Assert.AreEqual(BuiltinTypes.Int, forRange.Key!.Type);
        Assert.AreEqual(BuiltinTypes.Int, forRange.Value!.Type);
    }

    [TestMethod]
    public void Bind_for_range_slice_key_only()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
    for k := range s {
        x := k
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForRangeStatement>(fn.Body.Statements[1]);
        var forRange = (ForRangeStatement)fn.Body.Statements[1];
        Assert.IsNotNull(forRange.Key);
        Assert.IsNull(forRange.Value);
    }

    [TestMethod]
    public void Bind_for_range_bare()
    {
        var result = Analyze(@"package main
func main() {
    s := []int{1, 2, 3}
    for range s {
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForRangeStatement>(fn.Body.Statements[1]);
        var forRange = (ForRangeStatement)fn.Body.Statements[1];
        Assert.IsNull(forRange.Key);
        Assert.IsNull(forRange.Value);
    }

    [TestMethod]
    public void Bind_for_range_map()
    {
        var result = Analyze(@"package main
func main() {
    m := map[string]int{""a"": 1}
    for k, v := range m {
        s := k
        n := v
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForRangeStatement>(fn.Body.Statements[1]);
        var forRange = (ForRangeStatement)fn.Body.Statements[1];
        Assert.IsNotNull(forRange.Key);
        Assert.IsNotNull(forRange.Value);
        Assert.AreEqual(BuiltinTypes.String, forRange.Key!.Type);
        Assert.AreEqual(BuiltinTypes.Int, forRange.Value!.Type);
    }

    [TestMethod]
    public void Bind_for_range_string()
    {
        var result = Analyze(@"package main
func f(s string) {
    for k, v := range s {
        i := k
        r := v
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForRangeStatement>(fn.Body.Statements[0]);
        var forRange = (ForRangeStatement)fn.Body.Statements[0];
        Assert.AreEqual(BuiltinTypes.Int, forRange.Key!.Type);
        Assert.AreEqual(BuiltinTypes.Rune, forRange.Value!.Type);
    }

    [TestMethod]
    public void Bind_for_range_non_rangeable_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    for k := range 42 {
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidRange));
    }
}
