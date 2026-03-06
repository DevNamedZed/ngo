// -----------------------------------------------------------------------
// <copyright file="IndexExpressionSyntax.cs" company="Ziad">
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
    public sealed class IndexExpressionSyntax : ExpressionSyntax
    {
        public IndexExpressionSyntax(ExpressionSyntax expression, SyntaxToken openBracket, ExpressionSyntax index, SyntaxToken closeBracket)
        {
            Expression = expression;
            OpenBracket = openBracket;
            Index = index;
            CloseBracket = closeBracket;
        }

        public ExpressionSyntax Expression { get; }
        public SyntaxToken OpenBracket { get; }
        public ExpressionSyntax Index { get; }
        public SyntaxToken CloseBracket { get; }

        public override SyntaxKind Kind => SyntaxKind.IndexExpression;

        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return Expression;
            yield return OpenBracket;
            yield return Index;
            yield return CloseBracket;
        }
    }
}
