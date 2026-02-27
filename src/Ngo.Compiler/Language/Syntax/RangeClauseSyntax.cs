// -----------------------------------------------------------------------
// <copyright file="RangeClauseSyntax.cs" company="Ziad">
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
    public sealed class RangeClauseSyntax : SyntaxNode
    {
        public RangeClauseSyntax(SeparatedSyntaxList<ExpressionSyntax>? variables,
            SyntaxToken? assignOrDeclare, SyntaxToken rangeKeyword, ExpressionSyntax expression)
        { Variables = variables; AssignOrDeclare = assignOrDeclare; RangeKeyword = rangeKeyword; Expression = expression; }
        /// <summary>Key/value variables (may be null for bare range).</summary>
        public SeparatedSyntaxList<ExpressionSyntax>? Variables { get; }
        /// <summary>= or := token.</summary>
        public SyntaxToken? AssignOrDeclare { get; }
        public SyntaxToken RangeKeyword { get; }
        public ExpressionSyntax Expression { get; }
        public override SyntaxKind Kind => SyntaxKind.RangeClause;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            if (Variables.HasValue)
                foreach (var node in Variables.Value.GetWithSeparators()) yield return node;
            if (AssignOrDeclare != null) yield return AssignOrDeclare;
            yield return RangeKeyword; yield return Expression;
        }
    }
}
