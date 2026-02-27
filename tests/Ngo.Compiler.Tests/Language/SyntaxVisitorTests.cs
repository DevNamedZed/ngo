// -----------------------------------------------------------------------
// <copyright file="SyntaxVisitorTests.cs" company="Ziad">
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
public class SyntaxVisitorTests
{
    // A visitor that collects all visited SyntaxKinds in order.
    private sealed class KindCollector : SyntaxVisitor
    {
        public List<SyntaxKind> Kinds { get; } = new();

        protected override void DefaultVisit(SyntaxNode node)
        {
            Kinds.Add(node.Kind);
            base.DefaultVisit(node);
        }

        protected override void VisitToken(SyntaxToken token)
        {
            Kinds.Add(token.Kind);
        }
    }

    // A visitor that counts specific node types.
    private sealed class NodeCounter : SyntaxVisitor
    {
        public int FunctionCount { get; private set; }
        public int IdentifierCount { get; private set; }
        public int BinaryExpressionCount { get; private set; }
        public int LiteralCount { get; private set; }

        protected override void VisitFunctionDeclaration(FunctionDeclarationSyntax node)
        {
            FunctionCount++;
            base.DefaultVisit(node);
        }

        protected override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            IdentifierCount++;
            base.DefaultVisit(node);
        }

        protected override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            BinaryExpressionCount++;
            base.DefaultVisit(node);
        }

        protected override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            LiteralCount++;
            base.DefaultVisit(node);
        }
    }

    // A generic visitor that collects identifiers.
    private sealed class IdentifierCollector : SyntaxVisitor<List<string>>
    {
        protected override List<string>? DefaultVisit(SyntaxNode node)
        {
            var result = new List<string>();
            foreach (var child in node.ChildNodes())
            {
                var childResult = Visit(child);
                if (childResult != null)
                    result.AddRange(childResult);
            }
            return result;
        }

        protected override List<string>? VisitIdentifierName(IdentifierNameSyntax node)
        {
            return new List<string> { node.Identifier.Text };
        }

        protected override List<string>? VisitToken(SyntaxToken token) => null;
    }

    [TestMethod]
    public void Visit_null_does_nothing()
    {
        var collector = new KindCollector();
        collector.Visit(null);
        Assert.AreEqual(0, collector.Kinds.Count);
    }

    [TestMethod]
    public void Visit_traverses_all_nodes_in_tree()
    {
        var tree = SyntaxTree.Parse("package main");
        var collector = new KindCollector();
        collector.Visit(tree.Root);

        Assert.IsTrue(collector.Kinds.Contains(SyntaxKind.SourceFile));
        Assert.IsTrue(collector.Kinds.Contains(SyntaxKind.PackageClause));
        Assert.IsTrue(collector.Kinds.Contains(SyntaxKind.PackageKeyword));
        Assert.IsTrue(collector.Kinds.Contains(SyntaxKind.IdentifierToken));
        Assert.IsTrue(collector.Kinds.Contains(SyntaxKind.EndOfFileToken));
    }

    [TestMethod]
    public void Visit_dispatches_to_specific_methods()
    {
        var tree = SyntaxTree.Parse("package main\nfunc add() { return 1 + 2 }");
        var counter = new NodeCounter();
        counter.Visit(tree.Root);

        Assert.AreEqual(1, counter.FunctionCount);
        Assert.AreEqual(1, counter.BinaryExpressionCount);
        Assert.AreEqual(2, counter.LiteralCount);
    }

    [TestMethod]
    public void Visit_traverses_function_with_expression()
    {
        var tree = SyntaxTree.Parse("package main\nfunc foo() { x + y }");
        var counter = new NodeCounter();
        counter.Visit(tree.Root);

        Assert.AreEqual(1, counter.FunctionCount);
        Assert.AreEqual(1, counter.BinaryExpressionCount);
        Assert.AreEqual(2, counter.IdentifierCount);
    }

    [TestMethod]
    public void Visit_traverses_multiple_functions()
    {
        var tree = SyntaxTree.Parse("package main\nfunc a() {}\nfunc b() {}");
        var counter = new NodeCounter();
        counter.Visit(tree.Root);

        Assert.AreEqual(2, counter.FunctionCount);
    }

    [TestMethod]
    public void Generic_visitor_collects_identifiers()
    {
        var tree = SyntaxTree.Parse("package main\nfunc foo() { x + y }");
        var collector = new IdentifierCollector();
        var identifiers = collector.Visit(tree.Root);

        Assert.IsNotNull(identifiers);
        Assert.IsTrue(identifiers.Contains("x"));
        Assert.IsTrue(identifiers.Contains("y"));
    }

    [TestMethod]
    public void Generic_visitor_returns_null_for_null_node()
    {
        var collector = new IdentifierCollector();
        var result = collector.Visit(null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Visit_handles_nested_expressions()
    {
        var tree = SyntaxTree.Parse("package main\nfunc f() { (a + b) + c }");
        var counter = new NodeCounter();
        counter.Visit(tree.Root);

        Assert.AreEqual(2, counter.BinaryExpressionCount);
        Assert.AreEqual(3, counter.IdentifierCount);
    }
}
