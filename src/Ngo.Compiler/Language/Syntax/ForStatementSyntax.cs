// -----------------------------------------------------------------------
// <copyright file="ForStatementSyntax.cs" company="Ziad">
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
using Ngo.Compiler.Language;

namespace Ngo.Compiler.Language.Syntax
{
    public sealed class ForStatementSyntax : SyntaxNode
    {
        public ForStatementSyntax(SyntaxToken forKeyword, SyntaxNode? init, SyntaxToken? semicolon1,
            ExpressionSyntax? condition, SyntaxToken? semicolon2, SyntaxNode? post,
            RangeClauseSyntax? rangeClause, BlockSyntax body)
        {
            ForKeyword = forKeyword; Init = init; Semicolon1 = semicolon1;
            Condition = condition; Semicolon2 = semicolon2; Post = post;
            RangeClause = rangeClause; Body = body;
        }
        public SyntaxToken ForKeyword { get; }
        /// <summary>Init statement in for-clause (C-style for).</summary>
        public SyntaxNode? Init { get; }
        public SyntaxToken? Semicolon1 { get; }
        /// <summary>Condition expression (for-clause or simple for-condition).</summary>
        public ExpressionSyntax? Condition { get; }
        public SyntaxToken? Semicolon2 { get; }
        /// <summary>Post statement in for-clause.</summary>
        public SyntaxNode? Post { get; }
        /// <summary>Range clause (for-range form).</summary>
        public RangeClauseSyntax? RangeClause { get; }
        public BlockSyntax Body { get; }
        public override SyntaxKind Kind => SyntaxKind.ForStatement;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return ForKeyword;
            if (Init != null) yield return Init;
            if (Semicolon1 != null) yield return Semicolon1;
            if (Condition != null) yield return Condition;
            if (Semicolon2 != null) yield return Semicolon2;
            if (Post != null) yield return Post;
            if (RangeClause != null) yield return RangeClause;
            yield return Body;
        }
    }
}
