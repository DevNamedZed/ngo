// -----------------------------------------------------------------------
// <copyright file="SwitchStatementSyntax.cs" company="Ziad">
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
    public sealed class SwitchStatementSyntax : SyntaxNode
    {
        public SwitchStatementSyntax(SyntaxToken switchKeyword, SyntaxNode? init,
            SyntaxToken? initSemicolon, ExpressionSyntax? tag, SyntaxToken openBrace,
            IReadOnlyList<ExprSwitchCaseSyntax> cases, SyntaxToken closeBrace)
        { SwitchKeyword = switchKeyword; Init = init; InitSemicolon = initSemicolon;
          Tag = tag; OpenBrace = openBrace; Cases = cases; CloseBrace = closeBrace; }
        public SyntaxToken SwitchKeyword { get; }
        public SyntaxNode? Init { get; }
        public SyntaxToken? InitSemicolon { get; }
        public ExpressionSyntax? Tag { get; }
        public SyntaxToken OpenBrace { get; }
        public IReadOnlyList<ExprSwitchCaseSyntax> Cases { get; }
        public SyntaxToken CloseBrace { get; }
        public override SyntaxKind Kind => SyntaxKind.SwitchStatement;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            yield return SwitchKeyword;
            if (Init != null) yield return Init;
            if (InitSemicolon != null) yield return InitSemicolon;
            if (Tag != null) yield return Tag;
            yield return OpenBrace;
            foreach (var c in Cases) yield return c;
            yield return CloseBrace;
        }
    }
}
