// -----------------------------------------------------------------------
// <copyright file="IfStatementSyntax.cs" company="Ziad">
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
    public sealed class IfStatementSyntax : SyntaxNode
    {
        public IfStatementSyntax(SyntaxToken ifKeyword, SyntaxNode? init, SyntaxToken? initSemicolon,
            ExpressionSyntax condition, BlockSyntax body, SyntaxToken? elseKeyword, SyntaxNode? elseBody)
        { IfKeyword = ifKeyword; Init = init; InitSemicolon = initSemicolon; Condition = condition;
          Body = body; ElseKeyword = elseKeyword; ElseBody = elseBody; }
        public SyntaxToken IfKeyword { get; }
        public SyntaxNode? Init { get; }
        public SyntaxToken? InitSemicolon { get; }
        public ExpressionSyntax Condition { get; }
        public BlockSyntax Body { get; }
        public SyntaxToken? ElseKeyword { get; }
        /// <summary>BlockSyntax or IfStatementSyntax (else if).</summary>
        public SyntaxNode? ElseBody { get; }
        public override SyntaxKind Kind => SyntaxKind.IfStatement;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return IfKeyword;
            if (Init != null) yield return Init;
            if (InitSemicolon != null) yield return InitSemicolon;
            yield return Condition; yield return Body;
            if (ElseKeyword != null) yield return ElseKeyword;
            if (ElseBody != null) yield return ElseBody;
        }
    }
}
