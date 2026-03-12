// -----------------------------------------------------------------------
// <copyright file="AnalysisControlFlowTests.cs" company="Ziad">
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
public class AnalysisControlFlowTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
    }

    // ---- If statements ----

    [TestMethod]
    public void Bind_if_with_literal_condition()
    {
        var result = Analyze(@"package main
func f() int {
    if true {
        return 1
    }
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsNull(ifStmt.Init);
        Assert.IsNull(ifStmt.ElseBody);
        Assert.IsTrue(ifStmt.Body.Statements.Count > 0);
    }

    [TestMethod]
    public void Bind_if_with_comparison_condition()
    {
        var result = Analyze(@"package main
func f(x int) int {
    if x > 0 {
        return x
    }
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<BinaryExpression>(ifStmt.Condition);
    }

    [TestMethod]
    public void Bind_if_with_init_statement()
    {
        var result = Analyze(@"package main
func f() int {
    if x := 10; x > 0 {
        return x
    }
    return 0
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsNotNull(ifStmt.Init);
        Assert.IsInstanceOfType<VarDeclaration>(ifStmt.Init);
    }

    [TestMethod]
    public void Bind_if_else()
    {
        var result = Analyze(@"package main
func abs(x int) int {
    if x > 0 {
        return x
    } else {
        return -x
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsNotNull(ifStmt.ElseBody);
        Assert.IsInstanceOfType<BlockStatement>(ifStmt.ElseBody);
    }

    [TestMethod]
    public void Bind_if_else_if_chain()
    {
        var result = Analyze(@"package main
func classify(x int) int {
    if x > 0 {
        return 1
    } else if x < 0 {
        return -1
    } else {
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<IfStatement>(ifStmt.ElseBody);
        var elseIf = (IfStatement)ifStmt.ElseBody;
        Assert.IsNotNull(elseIf.ElseBody);
        Assert.IsInstanceOfType<BlockStatement>(elseIf.ElseBody);
    }

    [TestMethod]
    public void Bind_if_non_bool_condition_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    if 42 {
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_if_init_variable_scoped_to_if()
    {
        var result = Analyze(@"package main
func f() int {
    if x := 5; x > 0 {
        return x
    }
    return x
}");
        // x should be undeclared outside the if
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName));
    }

    // ---- For loops ----

    [TestMethod]
    public void Bind_for_infinite_loop()
    {
        var result = Analyze(@"package main
func f() {
    for {
        break
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[0]);
        var forStmt = (ForStatement)fn.Body.Statements[0];
        Assert.IsNull(forStmt.Init);
        Assert.IsNull(forStmt.Condition);
        Assert.IsNull(forStmt.Post);
    }

    [TestMethod]
    public void Bind_for_with_condition()
    {
        var result = Analyze(@"package main
func f(x int) int {
    for x > 0 {
        x--
    }
    return x
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[0]);
        var forStmt = (ForStatement)fn.Body.Statements[0];
        Assert.IsNull(forStmt.Init);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNull(forStmt.Post);
    }

    [TestMethod]
    public void Bind_for_three_clause()
    {
        var result = Analyze(@"package main
func f() int {
    sum := 0
    for i := 0; i < 10; i++ {
        sum = sum + i
    }
    return sum
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[1]);
        var forStmt = (ForStatement)fn.Body.Statements[1];
        Assert.IsNotNull(forStmt.Init);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Post);
    }

    [TestMethod]
    public void Bind_for_condition_type_mismatch()
    {
        var result = Analyze(@"package main
func f() {
    for 42 {
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_for_init_variable_scoped_to_for()
    {
        var result = Analyze(@"package main
func f() int {
    for i := 0; i < 10; i++ {
    }
    return i
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName));
    }

    // ---- Switch statements ----

    [TestMethod]
    public void Bind_switch_with_tag()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    case 1:
        return 10
    case 2:
        return 20
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.IsNotNull(sw.Tag);
        Assert.AreEqual(3, sw.Cases.Count);
        Assert.IsTrue(sw.Cases[2].IsDefault);
    }

    [TestMethod]
    public void Bind_switch_tagless()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch {
    case x > 0:
        return 1
    case x < 0:
        return -1
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.IsNull(sw.Tag);
        Assert.AreEqual(3, sw.Cases.Count);
    }

    [TestMethod]
    public void Bind_switch_with_init()
    {
        var result = Analyze(@"package main
func f() int {
    switch x := 5; x {
    case 5:
        return 1
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.IsNotNull(sw.Init);
    }

    [TestMethod]
    public void Bind_switch_case_type_mismatch()
    {
        var result = Analyze(@"package main
func f(x int) {
    switch x {
    case ""hello"":
        return
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.TypeMismatch));
    }

    [TestMethod]
    public void Bind_switch_default_case()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.AreEqual(1, sw.Cases.Count);
        Assert.IsTrue(sw.Cases[0].IsDefault);
        Assert.IsNull(sw.Cases[0].Expressions);
    }

    [TestMethod]
    public void Bind_switch_multiple_case_expressions()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    case 1, 2, 3:
        return 10
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.AreEqual(3, sw.Cases[0].Expressions!.Count);
    }

    // ---- Branch statements ----

    [TestMethod]
    public void Bind_break_statement()
    {
        var result = Analyze(@"package main
func f() {
    for {
        break
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[0]);
        var forStmt = (ForStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<BranchStatement>(forStmt.Body.Statements[0]);
        var branch = (BranchStatement)forStmt.Body.Statements[0];
        Assert.AreEqual(BranchKind.Break, branch.BranchKind);
        Assert.IsNull(branch.Label);
    }

    [TestMethod]
    public void Bind_continue_statement()
    {
        var result = Analyze(@"package main
func f() {
    for {
        continue
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[0]);
        var forStmt = (ForStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<BranchStatement>(forStmt.Body.Statements[0]);
        var branch = (BranchStatement)forStmt.Body.Statements[0];
        Assert.AreEqual(BranchKind.Continue, branch.BranchKind);
    }

    [TestMethod]
    public void Bind_fallthrough_statement()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    case 1:
        fallthrough
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<SwitchStatement>(fn.Body.Statements[0]);
        var sw = (SwitchStatement)fn.Body.Statements[0];
        Assert.IsInstanceOfType<BranchStatement>(sw.Cases[0].Body[0]);
        var branch = (BranchStatement)sw.Cases[0].Body[0];
        Assert.AreEqual(BranchKind.Fallthrough, branch.BranchKind);
    }

    // ---- Nested / combined ----

    [TestMethod]
    public void Bind_nested_if_inside_for()
    {
        var result = Analyze(@"package main
func f() int {
    sum := 0
    for i := 0; i < 10; i++ {
        if i > 5 {
            sum = sum + i
        }
    }
    return sum
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[1]);
        var forStmt = (ForStatement)fn.Body.Statements[1];
        Assert.IsInstanceOfType<IfStatement>(forStmt.Body.Statements[0]);
        var ifStmt = (IfStatement)forStmt.Body.Statements[0];
        Assert.IsNotNull(ifStmt.Condition);
    }

    [TestMethod]
    public void Bind_switch_inside_for()
    {
        var result = Analyze(@"package main
func f() int {
    result := 0
    for i := 0; i < 5; i++ {
        switch i {
        case 0:
            result = result + 1
        case 1:
            result = result + 2
        default:
            result = result + 3
        }
    }
    return result
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<ForStatement>(fn.Body.Statements[1]);
        var forStmt = (ForStatement)fn.Body.Statements[1];
        Assert.IsInstanceOfType<SwitchStatement>(forStmt.Body.Statements[0]);
        var sw = (SwitchStatement)forStmt.Body.Statements[0];
        Assert.AreEqual(3, sw.Cases.Count);
    }

    [TestMethod]
    public void Bind_for_with_break_and_continue()
    {
        var result = Analyze(@"package main
func f() int {
    sum := 0
    for i := 0; i < 100; i++ {
        if i > 50 {
            break
        }
        if i < 10 {
            continue
        }
        sum = sum + i
    }
    return sum
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Bind_if_else_if_with_init()
    {
        var result = Analyze(@"package main
func f(n int) int {
    if x := n * 2; x > 10 {
        return x
    } else if x > 5 {
        return x + 1
    } else {
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
        var fn = result.Root.Functions[0];
        Assert.IsInstanceOfType<IfStatement>(fn.Body.Statements[0]);
        var ifStmt = (IfStatement)fn.Body.Statements[0];
        Assert.IsNotNull(ifStmt.Init);
        // The x from init should be visible in else-if branches
        Assert.IsInstanceOfType<IfStatement>(ifStmt.ElseBody);
        var elseIf = (IfStatement)ifStmt.ElseBody;
        Assert.IsNotNull(elseIf.ElseBody);
    }

    [TestMethod]
    public void Bind_switch_init_variable_scoped()
    {
        var result = Analyze(@"package main
func f() int {
    switch x := 5; x {
    case 5:
        return x
    }
    return x
}");
        // x should be undeclared outside the switch
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName));
    }

    // ---- Branch validation ----

    [TestMethod]
    public void Break_outside_loop_or_switch_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    break
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidBranch));
    }

    [TestMethod]
    public void Continue_outside_loop_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    continue
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidBranch));
    }

    [TestMethod]
    public void Fallthrough_outside_switch_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    fallthrough
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidBranch));
    }

    [TestMethod]
    public void Break_inside_switch_is_valid()
    {
        var result = Analyze(@"package main
func main() {
    switch 1 {
    case 1:
        break
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Continue_inside_for_is_valid()
    {
        var result = Analyze(@"package main
func main() {
    for i := 0; i < 10; i++ {
        continue
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Continue_inside_switch_inside_for_is_valid()
    {
        var result = Analyze(@"package main
func main() {
    for i := 0; i < 10; i++ {
        switch i {
        case 1:
            continue
        }
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Continue_inside_switch_without_for_reports_error()
    {
        var result = Analyze(@"package main
func main() {
    switch 1 {
    case 1:
        continue
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.InvalidBranch));
    }

    [TestMethod]
    public void Break_in_nested_for_loops_is_valid()
    {
        var result = Analyze(@"package main
func main() {
    for i := 0; i < 10; i++ {
        for j := 0; j < 10; j++ {
            break
        }
        break
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    // ---- Missing return detection ----

    [TestMethod]
    public void Function_with_return_type_no_return_reports_error()
    {
        var result = Analyze(@"package main
func f() int {
    _ = 42
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }

    [TestMethod]
    public void If_without_else_missing_return_reports_error()
    {
        var result = Analyze(@"package main
func f(x int) int {
    if x > 0 {
        return 1
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }

    [TestMethod]
    public void If_else_both_return_no_error()
    {
        var result = Analyze(@"package main
func f(x int) int {
    if x > 0 {
        return 1
    } else {
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Switch_all_cases_and_default_return_no_error()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    case 1:
        return 10
    case 2:
        return 20
    default:
        return 0
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Switch_without_default_reports_missing_return()
    {
        var result = Analyze(@"package main
func f(x int) int {
    switch x {
    case 1:
        return 10
    case 2:
        return 20
    }
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }

    [TestMethod]
    public void Infinite_for_loop_no_missing_return()
    {
        var result = Analyze(@"package main
func f() int {
    for {
        return 1
    }
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Void_function_no_return_needed()
    {
        var result = Analyze(@"package main
func f() {
    _ = 42
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Method_missing_return_reports_error()
    {
        var result = Analyze(@"package main
type Foo struct{}
func (f Foo) Bar() int {
    _ = 42
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.MissingReturn));
    }

    // ---- Unreachable code detection ----

    [TestMethod]
    public void Statement_after_return_warns_unreachable()
    {
        var result = Analyze(@"package main
func f() {
    return
    x := 1
    _ = x
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Statement_after_break_warns_unreachable()
    {
        var result = Analyze(@"package main
func f() {
    for {
        break
        x := 1
        _ = x
    }
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Statement_after_continue_warns_unreachable()
    {
        var result = Analyze(@"package main
func f() {
    for {
        continue
        x := 1
        _ = x
    }
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Statement_after_goto_warns_unreachable()
    {
        var result = Analyze(@"package main
func f() {
    goto end
    x := 1
    _ = x
end:
    return
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Statement_after_fallthrough_warns_unreachable()
    {
        var result = Analyze(@"package main
func f(x int) {
    switch x {
    case 1:
        fallthrough
        x = 2
    default:
        return
    }
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Statement_after_if_else_both_return_warns_unreachable()
    {
        var result = Analyze(@"package main
func f(x int) {
    if x > 0 {
        return
    } else {
        return
    }
    x = 1
    _ = x
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void No_warning_after_if_without_else()
    {
        var result = Analyze(@"package main
func f(x int) {
    if x > 0 {
        return
    }
    x = 1
    _ = x
}");
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UnreachableCode));
    }

    [TestMethod]
    public void No_warning_for_valid_code()
    {
        var result = Analyze(@"package main
func f(x int) int {
    y := x + 1
    return y
}");
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UnreachableCode));
    }

    [TestMethod]
    public void No_warning_after_for_with_condition()
    {
        var result = Analyze(@"package main
func f() {
    for i := 0; i < 10; i++ {
        break
    }
    x := 1
    _ = x
}");
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UnreachableCode));
    }

    [TestMethod]
    public void Statement_after_infinite_for_warns_unreachable()
    {
        var result = Analyze(@"package main
func f() {
    for {
    }
    x := 1
    _ = x
}");
        Assert.IsTrue(result.Errors.Any(e =>
            e.Code == ErrorCode.UnreachableCode && e.Severity == ErrorSeverity.Warning));
    }

    [TestMethod]
    public void Only_first_unreachable_statement_warned()
    {
        var result = Analyze(@"package main
func f() {
    return
    x := 1
    y := 2
    _ = x
    _ = y
}");
        var warnings = result.Errors.Where(e => e.Code == ErrorCode.UnreachableCode).ToList();
        Assert.AreEqual(1, warnings.Count);
    }

    // ── Goto validation tests ──

    [TestMethod]
    public void Goto_undefined_label_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    goto missing
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndefinedLabel));
    }

    [TestMethod]
    public void Goto_defined_label_no_error()
    {
        var result = Analyze(@"package main
func f() {
    goto done
done:
    return
}");
        Assert.IsFalse(result.Errors.Any(e => e.Code == ErrorCode.UndefinedLabel));
    }

    [TestMethod]
    public void Goto_duplicate_label_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    goto done
done:
    return
done:
    return
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.DuplicateLabel));
    }

    [TestMethod]
    public void Goto_jumps_over_variable_declaration_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    goto end
    x := 1
    _ = x
end:
    return
}");
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.GotoJumpsOverDeclaration));
    }

    [TestMethod]
    public void Goto_backward_does_not_report_jump_over_decl()
    {
        var result = Analyze(@"package main
func f() {
    i := 0
again:
    i = i + 1
    if i < 5 {
        goto again
    }
    _ = i
}");
        Assert.IsFalse(result.Errors.Any(e =>
            e.Code == ErrorCode.GotoJumpsOverDeclaration));
    }

    [TestMethod]
    public void Goto_jumps_into_if_block_reports_error()
    {
        var result = Analyze(@"package main
func f(x int) {
    goto inside
    if x > 0 {
inside:
        x = 1
    }
    _ = x
}");
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.GotoJumpsIntoBlock));
    }

    [TestMethod]
    public void Goto_jumps_into_for_block_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    goto inside
    for i := 0; i < 10; i++ {
inside:
        _ = i
    }
}");
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.GotoJumpsIntoBlock));
    }

    [TestMethod]
    public void Goto_from_inner_to_outer_scope_is_valid()
    {
        var result = Analyze(@"package main
func f(x int) {
    if x > 0 {
        goto done
    }
done:
    return
}");
        Assert.IsFalse(result.Errors.Any(e =>
            e.Code == ErrorCode.GotoJumpsIntoBlock
            || e.Code == ErrorCode.UndefinedLabel));
    }

    [TestMethod]
    public void Goto_forward_no_decls_between_is_valid()
    {
        var result = Analyze(@"package main
func f() {
    x := 1
    goto end
    x = 2
end:
    _ = x
}");
        Assert.IsFalse(result.Errors.Any(e =>
            e.Code == ErrorCode.GotoJumpsOverDeclaration));
    }

    [TestMethod]
    public void Goto_jumps_over_var_declaration_reports_error()
    {
        var result = Analyze(@"package main
func f() {
    goto end
    var x int
    _ = x
end:
    return
}");
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.GotoJumpsOverDeclaration));
    }
}
