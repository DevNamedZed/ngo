// -----------------------------------------------------------------------
// <copyright file="CommClauseSyntax.cs" company="Ziad">
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
    public sealed class CommClauseSyntax : SyntaxNode
    {
        public CommClauseSyntax(SyntaxToken caseOrDefault, SyntaxNode? commStatement,
            SyntaxToken colon, IReadOnlyList<SyntaxNode> statements)
        { CaseOrDefault = caseOrDefault; CommStatement = commStatement; Colon = colon; Statements = statements; }
        public SyntaxToken CaseOrDefault { get; }
        /// <summary>Send or receive statement (null for default).</summary>
        public SyntaxNode? CommStatement { get; }
        public SyntaxToken Colon { get; }
        public IReadOnlyList<SyntaxNode> Statements { get; }
        public override SyntaxKind Kind => SyntaxKind.CommClause;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return CaseOrDefault;
            if (CommStatement != null) yield return CommStatement;
            yield return Colon;
            foreach (var stmt in Statements) yield return stmt;
        }
    }
}
