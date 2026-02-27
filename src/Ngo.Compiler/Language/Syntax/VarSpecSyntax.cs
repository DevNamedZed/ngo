// -----------------------------------------------------------------------
// <copyright file="VarSpecSyntax.cs" company="Ziad">
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
    public sealed class VarSpecSyntax : SyntaxNode
    {
        public VarSpecSyntax(SeparatedSyntaxList<SyntaxToken> names,
            ExpressionSyntax? type, SyntaxToken? equalsToken,
            SeparatedSyntaxList<ExpressionSyntax>? values)
        { Names = names; Type = type; EqualsToken = equalsToken; Values = values; }
        public SeparatedSyntaxList<SyntaxToken> Names { get; }
        public ExpressionSyntax? Type { get; }
        public SyntaxToken? EqualsToken { get; }
        public SeparatedSyntaxList<ExpressionSyntax>? Values { get; }
        public override SyntaxKind Kind => SyntaxKind.VarSpec;
        public override IEnumerable<SyntaxNode> ChildNodes()
        {
            foreach (var node in Names.GetWithSeparators()) yield return node;
            if (Type != null) yield return Type;
            if (EqualsToken != null) yield return EqualsToken;
            if (Values.HasValue)
                foreach (var node in Values.Value.GetWithSeparators()) yield return node;
        }
    }
}
