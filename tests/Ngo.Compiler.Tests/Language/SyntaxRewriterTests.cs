// -----------------------------------------------------------------------
// <copyright file="SyntaxRewriterTests.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Linq;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Language;

[TestClass]
public class SyntaxRewriterTests
{
    // Identity rewriter — returns same tree unchanged.
    private sealed class IdentityRewriter : SyntaxRewriter
    {
    }

    // Rewriter that replaces identifier names.
    private sealed class RenameRewriter : SyntaxRewriter
    {
        private readonly string _from;
        private readonly string _to;

        public RenameRewriter(string from, string to) { _from = from; _to = to; }

        protected override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == _from)
            {
                var newToken = new SyntaxToken(SyntaxKind.IdentifierToken, _to, node.Identifier.Position);
                return new IdentifierNameSyntax(newToken);
            }
            return node;
        }
    }

    // Rewriter that doubles all integer literals (e.g., 1 becomes 2, 5 becomes 10).
    private sealed class DoubleLiteralRewriter : SyntaxRewriter
    {
        protected override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.Token.Kind == SyntaxKind.IntLiteralToken && long.TryParse(node.Token.Text, out var val))
            {
                var doubled = val * 2;
                var newToken = new SyntaxToken(SyntaxKind.IntLiteralToken, doubled.ToString(),
                    node.Token.Position);
                return new LiteralExpressionSyntax(newToken);
            }
            return node;
        }
    }

    [TestMethod]
    public void Identity_rewriter_returns_same_root()
    {
        var tree = SyntaxTree.Parse("package main\nfunc foo() {}");
        var rewriter = new IdentityRewriter();
        var result = rewriter.Visit(tree.Root);
        Assert.AreSame(tree.Root, result);
    }

    [TestMethod]
    public void Identity_rewriter_preserves_function_declaration()
    {
        var tree = SyntaxTree.Parse("package main\nfunc add(a int, b int) int { return a + b }");
        var rewriter = new IdentityRewriter();
        var result = rewriter.Visit(tree.Root);
        Assert.AreSame(tree.Root, result);
    }

    [TestMethod]
    public void Rename_rewriter_produces_new_tree()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() { x + y }");
        var rewriter = new RenameRewriter("x", "z");
        var result = (SourceFileSyntax)rewriter.Visit(tree.Root)!;

        Assert.AreNotSame(tree.Root, result);

        // Collect all identifier texts from the rewritten tree
        var identifiers = result.DescendantTokens()
            .Where(t => t.Kind == SyntaxKind.IdentifierToken)
            .Select(t => t.Text)
            .ToList();

        Assert.IsTrue(identifiers.Contains("z"));
        Assert.IsFalse(identifiers.Contains("x"));
        Assert.IsTrue(identifiers.Contains("y"));
    }

    [TestMethod]
    public void Rename_rewriter_preserves_unchanged_subtrees()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() { x + y }");
        var rewriter = new RenameRewriter("x", "z");
        var result = (SourceFileSyntax)rewriter.Visit(tree.Root)!;

        // Package clause should be preserved (not rewritten)
        Assert.AreSame(tree.Root.PackageClause, result.PackageClause);
    }

    [TestMethod]
    public void Double_literal_rewriter_transforms_literals()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() { 1 + 2 }");
        var rewriter = new DoubleLiteralRewriter();
        var result = (SourceFileSyntax)rewriter.Visit(tree.Root)!;

        Assert.AreNotSame(tree.Root, result);

        var intLiterals = result.DescendantTokens()
            .Where(t => t.Kind == SyntaxKind.IntLiteralToken)
            .ToList();

        Assert.AreEqual(2, intLiterals.Count);
        Assert.AreEqual("2", intLiterals[0].Text);
        Assert.AreEqual("4", intLiterals[1].Text);
    }

    [TestMethod]
    public void Rewriter_handles_no_matching_nodes()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() {}");
        var rewriter = new RenameRewriter("nonexistent", "other");
        var result = rewriter.Visit(tree.Root);

        // No changes, should return same instance
        Assert.AreSame(tree.Root, result);
    }

    [TestMethod]
    public void Rewriter_handles_nested_expressions()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() { (x + y) + x }");
        var rewriter = new RenameRewriter("x", "z");
        var result = (SourceFileSyntax)rewriter.Visit(tree.Root)!;

        var identifiers = result.DescendantTokens()
            .Where(t => t.Kind == SyntaxKind.IdentifierToken)
            .Select(t => t.Text)
            .ToList();

        // Both x's should be renamed to z
        Assert.IsFalse(identifiers.Contains("x"));
        var zCount = identifiers.Count(id => id == "z");
        Assert.AreEqual(2, zCount);
    }

    [TestMethod]
    public void Identity_rewriter_preserves_complex_tree()
    {
        var source = @"package main

import ""fmt""

func main() {
    fmt.Println(1 + 2)
}";
        var tree = SyntaxTree.Parse(source);
        var rewriter = new IdentityRewriter();
        var result = rewriter.Visit(tree.Root);
        Assert.AreSame(tree.Root, result);
    }
}
