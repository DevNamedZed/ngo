// -----------------------------------------------------------------------
// <copyright file="SyntaxTreeTests.cs" company="Ziad">
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

namespace Ngo.Compiler.Tests.Language;

[TestClass]
public class SyntaxTreeTests
{
    [TestMethod]
    public void Parse_returns_root_with_package_clause()
    {
        var tree = SyntaxTree.Parse("package main");
        Assert.IsNotNull(tree.Root);
        Assert.AreEqual(SyntaxKind.PackageClause, tree.Root.PackageClause.Kind);
        Assert.AreEqual("main", tree.Root.PackageClause.Name.Text);
    }

    [TestMethod]
    public void Parse_preserves_source_text()
    {
        var source = "package main\nfunc foo() {}";
        var tree = SyntaxTree.Parse(source);
        Assert.AreEqual(source, tree.SourceText);
    }

    [TestMethod]
    public void Parse_valid_source_has_no_errors()
    {
        var tree = SyntaxTree.Parse("package main\nfunc foo() {}");
        Assert.IsFalse(tree.HasErrors);
        Assert.AreEqual(0, tree.Errors.Count);
    }

    [TestMethod]
    public void Parse_invalid_source_reports_errors()
    {
        // Missing package name
        var tree = SyntaxTree.Parse("package");
        Assert.IsTrue(tree.HasErrors);
        Assert.IsTrue(tree.Errors.Count > 0);
        Assert.IsTrue(tree.Errors.Any(e => e.Severity == ErrorSeverity.Error));
    }

    [TestMethod]
    public void Parse_returns_function_declarations()
    {
        var tree = SyntaxTree.Parse("package main\nfunc add(a int, b int) int { return a + b }");
        Assert.AreEqual(1, tree.Root.Members.Count);
        Assert.IsInstanceOfType<FunctionDeclarationSyntax>(tree.Root.Members[0]);
        var fn = (FunctionDeclarationSyntax)tree.Root.Members[0];
        Assert.AreEqual("add", fn.Name.Text);
    }

    [TestMethod]
    public void Errors_contain_code_and_message()
    {
        var tree = SyntaxTree.Parse("package");
        var error = tree.Errors.First(e => e.Severity == ErrorSeverity.Error);
        Assert.AreEqual(ErrorCode.TokenExpected, error.Code);
        Assert.IsNotNull(error.Message);
        Assert.IsTrue(error.Message.Length > 0);
    }

    [TestMethod]
    public void CompileError_ToString_includes_severity_and_code()
    {
        var tree = SyntaxTree.Parse("package");
        var error = tree.Errors.First(e => e.Severity == ErrorSeverity.Error);
        var text = error.ToString();
        StringAssert.Contains(text, "Error");
        StringAssert.Contains(text, "TokenExpected");
    }
}
