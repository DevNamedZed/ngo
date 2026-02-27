// -----------------------------------------------------------------------
// <copyright file="ExprSwitchCaseSyntax.cs" company="Ziad">
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
    public sealed class ExprSwitchCaseSyntax : SyntaxNode
    {
        public ExprSwitchCaseSyntax(SyntaxToken caseOrDefault,
            SeparatedSyntaxList<ExpressionSyntax>? expressions, SyntaxToken colon,
            IReadOnlyList<SyntaxNode> statements)
        { CaseOrDefault = caseOrDefault; Expressions = expressions; Colon = colon; Statements = statements; }
        public SyntaxToken CaseOrDefault { get; }
        public SeparatedSyntaxList<ExpressionSyntax>? Expressions { get; }
        public SyntaxToken Colon { get; }
        public IReadOnlyList<SyntaxNode> Statements { get; }
        public override SyntaxKind Kind => SyntaxKind.ExprSwitchCase;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return CaseOrDefault;
            if (Expressions.HasValue)
                foreach (var node in Expressions.Value.GetWithSeparators()) yield return node;
            yield return Colon;
            foreach (var stmt in Statements) yield return stmt;
        }
    }
}
